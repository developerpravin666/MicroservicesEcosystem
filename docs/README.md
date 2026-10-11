# Documentation

- [User manual](accounting-user-manual.md): step-by-step guidance for owners, purchase/sales staff and accountants.
- [Printable user manual](accounting-user-manual.html): standalone file to send to users; open in a browser and select Print / Save PDF.
- [Deployment and recovery](accounting-p0-release.md): administrator setup, backups, migrations and release acceptance.
- [Readiness and remaining work](accounting-inventory-readiness.md): implementation status and release requirements.

## Maintain and distribute the manual

The Markdown user manual is the source. Update its version/date and affected steps whenever a user-facing workflow changes, including form fields, menu names, permissions, validations, pricing, accounting behavior and printed documents. Check each procedure against the current screens and record the expected result. Describe only available behavior, including material limitations.

Regenerate the HTML from MicroservicesEcosystem:

```powershell
powershell -NoProfile -File ops/Export-UserManual.ps1
```

Open the generated HTML, check its contents links and print preview, then send that file or a PDF saved from the browser to users. The exporter handles the headings, paragraphs, lists, tables and inline formatting used in this manual. It is not a general Markdown renderer; review output when adding new formatting.

Keep developer/deployment commands in the administrator runbook. Add user instructions for new functionality before handing the release to users. Localized editions and screenshots should identify the application/manual version they cover.
