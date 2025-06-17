# Contributing to RhinoPlugin

Thank you for your interest in contributing to **RhinoPlugin**, the plugin companion for [Axys](https://github.com/Apollo-ARTE/Axys). We appreciate bug reports, feature requests, and code contributions.

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
3. Follow the pull request template and make sure to include:
   - What you changed
   - Why you did it
   - How to test it

Draft PRs are welcome if you'd like early feedback.

---

## Code Review Process

PRs will be reviewed by maintainers.  
Please be open to feedback and iterative improvement.
  
---

## Reporting Issues
You can create issues with the provided template for a more structured format, making sure to include:
- Device and visionOS version
- Steps to reproduce
- Error messages or logs
- Screenshots, if helpful

Check existing issues before submitting a new one.

---


## Credits

This project is developed and maintained by:

- [Ilia Sedelkin](https://www.linkedin.com/in/iliasedelkin)
- [Bruna Avellar](https://www.linkedin.com/in/brunaavellar)
- [Alessandro Bortoluzzi](https://bortoluzzi.dev)
- [Madina Malsagova](https://www.linkedin.com/in/madina-malsague)
- [Guillermo Kramsky](https://www.linkedin.com/in/guillermo-kramsky-5a9ba3246)
- [Salvatore Flauto](https://github.com/XlSolver)

We thank all contributors who help improve this project through their code, feedback, and ideas.
