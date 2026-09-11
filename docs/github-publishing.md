# Publish your copy to GitHub

The source folder is an ordinary Git repository. It includes code, tests, CI, release packaging, documentation and the MIT license. No remote repository or public upload is created by the local build.

Using GitHub CLI, from the source folder:

```powershell
gh auth login
gh repo create background-ferry --private --source . --remote origin --push
```

Choose `--public` instead if you want the project to be publicly readable. GitHub Desktop's **Add local repository** → **Publish repository** is another option.

Once the default-branch workflow passes, publish a release:

```powershell
git tag v0.2.1
git push origin v0.2.1
```

The release workflow builds and tests the source, creates a self-contained Windows x64 ZIP and a SHA-256 checksum, and attaches them to a GitHub Release. This workflow needs repository Actions enabled. Review its files under `.github/workflows` before publishing.

If the GitHub repository was created through the website, create it empty (without an extra README/license), add its URL with `git remote add origin <repository-url>`, and run `git push -u origin main`.
