# Playtests

At the end of each chapter, Claude Code writes `chapter-N.md` here and stops. The human plays, then writes `chapter-N-feedback.md`. The next session reads the feedback first and turns it into tasks before continuing.

## Report format (`chapter-N.md`, written by Claude Code)

```
# Playtest N — <chapter name>
Build: <commit hash>, <date/time IST>
How to run: npm run play   (opens http://localhost:8080)

## What's new
- ...

## Try this (10–20 minutes)
1. ...
2. ...

## Questions for you
1. ...   (5–8 specific questions, e.g. "At ▶▶▶, can you tell what's happening?")

## Known issues (don't report these)
- ...

## Numbers
- sim ms / fps from the overlay on my runs, test status, band results

## Screenshots
- out/shots/latest/... (list the interesting ones)
```

## Feedback format (`chapter-N-feedback.md`, written by the human)

Anything goes. Helpful structure:

```
# Feedback N
Machine/browser: ...
FPS I saw: ...

## Loved
## Confusing
## Broken
## Boring / cut
## Change requests (priority order)
```
