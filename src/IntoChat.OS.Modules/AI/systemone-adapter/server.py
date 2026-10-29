"""A local, LLM-backed stand-in for the TypeSafe System One API.

Exposes POST /v1/systemone with the same request and response shape as the hosted Jev API, but
answered by any OpenAI-compatible chat model. Point the brain's `typesafe` integration Endpoint at
this service to route through Jev's wire shape without the hosted model -- useful for offline
development and demos. The real Jev is a hosted model; this is the documented adapter shape.
"""

import json
import os
import threading

from fastapi import FastAPI, Request, Response
from openai import OpenAI

app = FastAPI(title="System One Adapter")

_client = OpenAI(
    base_url=os.environ.get("OPENAI_BASE_URL", "http://ollama:11434/v1"),
    api_key=os.environ.get("OPENAI_API_KEY", "not-needed"),
)
_MODEL = os.environ.get("SYSTEMONE_MODEL", "llama3.1")

_SYSTEM = (
    "You are a System One decision engine. Read the STATE and answer every QUESTION in parallel "
    "against it, each with a calibrated probability distribution.\n"
    "Reply with JSON only, no prose: {\"answers\": {\"<question id>\": <answer>}}.\n"
    "choice question -> {\"type\":\"choice\",\"choice\":\"<option>\",\"probabilities\":{\"<option>\":<p>,...},\"confidence\":<c>}\n"
    "score question  -> {\"type\":\"score\",\"score\":<0-based index>,\"probabilities\":{\"<index>\":<p>,...},\"confidence\":<c>}\n"
    "noul question   -> {\"type\":\"noul\",\"noul\":<0..1>}\n"
    "Probabilities sum to 1. confidence is 0..1 and higher when one option dominates."
)


def _normalize(answer: dict, question: dict) -> dict:
    kind = question.get("type")
    if kind == "choice":
        probabilities = answer.get("probabilities") or {}
        choice = answer.get("choice") or (max(probabilities, key=probabilities.get) if probabilities else "")
        confidence = answer.get("confidence")
        if confidence is None:
            confidence = probabilities.get(choice, 0.0)
        return {"type": "choice", "choice": choice, "probabilities": probabilities, "confidence": confidence}
    if kind == "score":
        probabilities = answer.get("probabilities") or {}
        score = answer.get("score", 0)
        confidence = answer.get("confidence")
        if confidence is None:
            confidence = probabilities.get(str(score), 0.0)
        return {"type": "score", "score": score, "probabilities": probabilities, "confidence": confidence}
    return {"type": "noul", "noul": answer.get("noul", 0.0)}


_warm = threading.Event()
_warming = threading.Lock()


def _warm_up() -> None:
    # One minimal inference loads the model; until it succeeds the resource is not healthy.
    try:
        _client.chat.completions.create(
            model=_MODEL,
            messages=[{"role": "user", "content": 'Reply with JSON {"answers": {"probe": "ok"}}'}],
            response_format={"type": "json_object"},
            temperature=0.0,
        )
        _warm.set()
    finally:
        _warming.release()


@app.get("/health")
def health() -> Response:
    if _warm.is_set():
        return Response(content='{"status": "ok"}', media_type="application/json")
    if _warming.acquire(blocking=False):
        threading.Thread(target=_warm_up, daemon=True).start()
    return Response(status_code=503, content='{"status": "warming"}', media_type="application/json")


@app.post("/v1/systemone")
async def systemone(request: Request) -> dict:
    body = await request.json()
    questions: dict = body.get("questions", {})
    model = body.get("model") or _MODEL
    user = "STATE:\n" + str(body.get("state", "")) + "\n\nQUESTIONS:\n" + json.dumps(questions, indent=2)
    completion = _client.chat.completions.create(
        model=model,
        messages=[{"role": "system", "content": _SYSTEM}, {"role": "user", "content": user}],
        response_format={"type": "json_object"},
        temperature=0.0,
    )
    raw = completion.choices[0].message.content or "{}"
    produced = json.loads(raw).get("answers", {})
    answers = {key: _normalize(produced.get(key, {}), question) for key, question in questions.items()}
    usage = getattr(completion, "usage", None)
    return {
        "model": model,
        "answers": answers,
        "usage": {
            "input_tokens": getattr(usage, "prompt_tokens", 0) if usage else 0,
            "output_tokens": getattr(usage, "completion_tokens", 0) if usage else 0,
        },
    }
