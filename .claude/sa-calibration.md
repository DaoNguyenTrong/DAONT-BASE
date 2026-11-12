# Solution-architect calibration log

Each time the user overrides an answer `solution-architect` gave on their behalf, the main session adds one entry here in the same PR as the corrected document, newest first. `solution-architect` reads this file before answering and treats each lesson as a rule about the user's taste. Entries are never edited or removed; a later entry may supersede an earlier one by saying so.

Format:

```
### YYYY-MM-DD: <short topic>
- SA answered: <what the SA decided, with the question number or ID>
- User decided: <what the user chose instead>
- Lesson: <the general rule to apply next time, one sentence>
```

No overrides recorded for DAONT-BASE under this workflow yet.
