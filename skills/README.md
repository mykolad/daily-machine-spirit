# Skills

**Consult the Machine Spirit** (`consult-the-machine-spirit/`): tell your coding assistant what's wrong ("the build
fails, then passes when I rerun it"), and it prescribes a published rite or two in a tech-priest's voice, links each
one's page, then offers to reveal its Heretical Truth, the real fix, if you dare. It reads the public rites API
(`docs/api.md`) and lets your own model choose, so your trouble never leaves your machine.

To use it with Claude Code, copy the folder into your skills:

```
mkdir -p ~/.claude/skills && cp -r skills/consult-the-machine-spirit ~/.claude/skills/    # every project
mkdir -p .claude/skills && cp -r skills/consult-the-machine-spirit .claude/skills/        # one project
```

(`mkdir -p` first: copied into a folder that doesn't exist yet, the skill's files would land in `skills/` itself, where
they aren't found.)

Then ask, for example, "Consult the Machine Spirit: my cache keeps serving the old page." It needs a way to fetch a URL
to a file (`curl`, or PowerShell's `Invoke-WebRequest`).
