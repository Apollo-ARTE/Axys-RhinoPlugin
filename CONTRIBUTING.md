# Contributing to RhinoPlugin

Thank you for your interest in contributing to **RhinoPlugin**, the plugin companion for [Axys](https://github.com/Apollo-ARTE/Axys)!  
We appreciate bug reports, feature requests, and code contributions that improve the plugin’s integration with Rhino and Vision Pro.

---

## Getting Started

### Fork the repository  
If you don’t have push rights, fork the repo first. Otherwise, branch directly.

### Create a branch  
```bash
git checkout -b feature/your-feature-name
```

---

## Branch Naming

We follow a **feature-branch-based workflow**, where each feature, bugfix, or task is developed in its own branch.

Please use **clear and consistent branch names** based on the purpose of the branch. The following prefixes help us keep the repository organized:

| Type        | Prefix     | Example                       |
|-------------|------------|-------------------------------|
| Feature     | `feat/`    | `feat/interactive-panel`      |
| UI Update   | `ui/`      | `ui/navigation-bar-style`     |
| Bug Fix     | `bugfix/`  | `bugfix/export-crash`         |
| Chore       | `chore/`   | `chore/update-dependencies`   |
| Docs        | `docs/`    | `docs/update-readme`          |
| Test Branch | `test/`    | `test/sandbox-ui-tests`       |
| Junk        | `junk/`    | `junk/prototype-camera`       |

> ⚠️ Branches with the `junk/` prefix are intended for **throwaway work** and should **never be merged**.

The `main` branch always reflects **stable, production-ready** code.

---

## Making a Pull Request

When your changes are ready:

1. Push your branch:
   ```bash
   git push origin feature/your-feature-name
   ```
2. Open a Pull Request (PR) on GitHub.
3. Describe your changes:
   - What was changed
   - Why it was needed
   - How it was tested

Draft PRs are welcome if you'd like early feedback.

---

## Code Review Process

Your PR will be reviewed by at least one core maintainer.  
Expect feedback and be ready to iterate.

### Best Practices
- **Stay respectful**: feedback is about the code, not the person.
- **Be specific**: reference code lines or include samples.
- **Be constructive**: explain reasoning behind suggestions.
- Tag minor style issues with `(nitpick)`.
- Use clean, focused commits.

### Reviewer Checklist
(Optional—can be copied to PR description):

```
## Review Checklist

- [ ] Changes address the stated issue/feature
- [ ] Code is maintainable and follows project structure
- [ ] Edge cases are handled
- [ ] Tests added or existing ones updated
- [ ] Functionality tested in Rhino 8
- [ ] Commit messages are clear
- [ ] Documentation/README updated where relevant
```

---

## Reporting Issues

When opening a bug report, please include:
- Rhino version and operating system
- Steps to reproduce
- Console logs or error messages
- Screenshots, if helpful

Search existing issues first to avoid duplicates.

---

## Credits

This project is developed and maintained by:

- The [Apollo ARTE](https://github.com/Apollo-ARTE) team
- [Salvatore Flauto](https://github.com/XlSolver)

We thank all contributors who help improve this project through their code, feedback, and ideas.
