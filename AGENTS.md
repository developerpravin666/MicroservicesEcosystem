# User documentation

When changing a user-facing workflow, update `docs/accounting-user-manual.md` in the same task. This includes menu/button labels, form fields, permissions, validation, financial calculations, reports and printing. Include step-by-step instructions, prerequisites, expected results and material limitations; verify the instructions against the implementation. Update the manual version/date when its content changes.

Run `powershell -NoProfile -File ops/Export-UserManual.ps1` to regenerate the shareable `docs/accounting-user-manual.html`. Keep user guidance separate from deployment/recovery instructions in `docs/accounting-p0-release.md`. See `docs/README.md` for distribution instructions.
