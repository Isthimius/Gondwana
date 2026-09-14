#!/usr/bin/env python3
"""Conflict-aware Wiki synchronization. Requires Git, gh, and Python 3.11+."""

import argparse
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import tempfile

BRANCH = "automation/wiki-sync"
TITLE = "docs(wiki): synchronize changes from GitHub Wiki"
STATE = "Tooling/scripts/wiki-sync/baseline.json"
PREFIX = "docs/wiki/"
README = "README.md"


def run(*args, cwd=None, data=None, check=True):
    result = subprocess.run(args, cwd=cwd, input=data, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE)
    if check and result.returncode:
        # Never echo arguments, environment, Git/HTTP diagnostics, or credentials.
        raise RuntimeError(f"{args[0]} failed (exit {result.returncode}); "
                           "check access, remote changes, or merge conflicts. No remote overwrite attempted.")
    return result


def git(repo, *args):
    return run("git", "-C", str(repo), *args).stdout


def ancestor(repo, older, newer):
    return run("git", "-C", str(repo), "merge-base", "--is-ancestor",
               older, newer, check=False).returncode == 0


def snapshot(repo, revision, prefix=""):
    """Read Git objects, not working-tree filters; retain exact bytes and modes."""
    entries = {}
    for entry in git(repo, "ls-tree", "-rz", revision).split(b"\0"):
        if not entry:
            continue
        metadata, raw_path = entry.split(b"\t", 1)
        mode, kind, sha = metadata.decode().split()
        path = raw_path.decode("utf-8")
        if not path.startswith(prefix):
            continue
        path = path[len(prefix):]
        if path == README:
            continue
        # Never allow Wiki content to write through symlinks or into Git internals.
        if (kind != "blob" or mode not in ("100644", "100755")
                or any(p.lower() == ".git" or p in ("..", "")
                       for p in PurePosixPath(path).parts)):
            raise RuntimeError(f"Unsupported Wiki entry: {path!r}")
        entries[path] = (mode, git(repo, "cat-file", "blob", sha))
    return entries


def combine(base, destination, source):
    """Whole-file three-way merge; absence represents deletion. Fail atomically."""
    merged = dict(destination)
    conflicts = []
    for path in sorted(base.keys() | destination.keys() | source.keys()):
        old, ours, theirs = base.get(path), destination.get(path), source.get(path)
        if ours == theirs or theirs == old:
            continue
        if ours != old:
            conflicts.append(path)
        elif theirs is None:
            merged.pop(path, None)
        else:
            merged[path] = theirs
    if conflicts:
        raise RuntimeError("Independent edits conflict; resolve explicitly with a reviewed "
                           "PR or manual authoritative sync: " + json.dumps(conflicts))
    return merged


def write_tree(repo, prefix, before, after):
    """Stage exact blobs through Git's index, including ignored assets and deletions."""
    for path in sorted(before.keys() | after.keys()):
        if path == README:
            raise RuntimeError("Repository README must never be synchronized")
        if before.get(path) == after.get(path):
            continue
        target = prefix + path
        if path not in after:
            git(repo, "update-index", "--force-remove", "--", target)
        else:
            mode, content = after[path]
            sha = run("git", "-C", str(repo), "hash-object", "-w", "--stdin",
                      data=content).stdout.decode().strip()
            git(repo, "update-index", "--add", "--cacheinfo", mode, sha, target)


def common_snapshot(repo, wiki, revision, wiki_head):
    """Choose the latest import or publish anchor on the current Wiki history.

    Import anchors refer to a Wiki tree. Publish anchors refer to the protected
    repository tree: the published Wiki may also contain unimported Wiki edits.
    Treating that entire Wiki tree as acknowledged would lose those edits.
    """
    state = json.loads(git(repo, "show", f"{revision}:{STATE}"))
    imported = state["wiki_commit"]
    if not re.fullmatch(r"[0-9a-f]{40}", imported) or not ancestor(wiki, imported, wiki_head):
        raise RuntimeError("Wiki history diverged from the import anchor; use explicit manual recovery")
    for commit in git(wiki, "rev-list", "--first-parent", wiki_head).decode().splitlines():
        if commit == imported or ancestor(wiki, commit, imported):
            break
        message = git(wiki, "show", "-s", "--format=%B", commit).decode()
        match = re.search(r"^Gondwana-Source: ([0-9a-f]{40})$", message, re.M)
        if match and ancestor(repo, match[1], "origin/master"):
            return snapshot(repo, match[1], PREFIX)
    return snapshot(wiki, imported)


def api(endpoint, method="GET", payload=None):
    args = ["gh", "api", endpoint, "--method", method]
    if payload is not None:
        args += ["--input", "-"]
    output = run(*args, data=json.dumps(payload).encode() if payload is not None else None).stdout
    return json.loads(output) if output else None


def open_pr(repository):
    owner = repository.split("/")[0]
    prs = api(f"repos/{repository}/pulls?state=open&base=master&head={owner}:{BRANCH}")
    if len(prs) > 1:
        raise RuntimeError("Multiple Wiki PRs exist; resolve duplicate PRs before retrying")
    return prs[0] if prs else None


def ensure_pr(repository, pr):
    if pr is None:
        pr = api(f"repos/{repository}/pulls", "POST", {
            "title": TITLE, "head": BRANCH, "base": "master",
            "body": "Synchronizes GitHub Wiki content through the protected master PR process.\n\n"
                    "Uses the shared automation/wiki-sync branch. Independent conflicting edits "
                    "fail visibly; normal checks and required human reviews govern merging.\n\n"
                    "docs(wiki): commits are excluded from git-cliff. The existing documentation "
                    "labeler excludes this PR from release notes. [skip release notes]"})
    elif not pr["title"].startswith("docs(wiki):"):
        api(f"repos/{repository}/pulls/{pr['number']}", "PATCH", {"title": TITLE})
    if pr.get("auto_merge") is None:
        # GraphQL only enables auto-merge; no admin flag, bypass, or direct merge.
        settings = api(f"repos/{repository}")
        method = next((name for name, key in (("SQUASH", "allow_squash_merge"),
                       ("MERGE", "allow_merge_commit"), ("REBASE", "allow_rebase_merge"))
                       if settings.get(key)), None)
        if method is None or not settings.get("allow_auto_merge"):
            raise RuntimeError("Wiki PR created, but repository auto-merge is unavailable")
        result = api("graphql", "POST", {
            "query": "mutation($id:ID!,$method:PullRequestMergeMethod!){"
                     "enablePullRequestAutoMerge(input:{pullRequestId:$id,mergeMethod:$method})"
                     "{pullRequest{id}}}",
            "variables": {"id": pr["node_id"], "method": method}})
        if result.get("errors"):
            raise RuntimeError("Wiki PR is open, but GitHub rejected enabling auto-merge; "
                               "inspect the PR and repository requirements")
    print(f"Wiki PR: {pr['html_url']}")


def synchronize(repo, wiki, direction, authoritative, repository):
    git(repo, "fetch", "origin", "+refs/heads/master:refs/remotes/origin/master")
    master = git(repo, "rev-parse", "origin/master").decode().strip()
    wiki_head = git(wiki, "rev-parse", "HEAD").decode().strip()
    remote_tree = snapshot(wiki, wiki_head)
    if direction == "repo-to-wiki":
        repo_tree = snapshot(repo, master, PREFIX)
        if repo_tree == remote_tree:
            print("Wiki already current; no commit.")
            return
        target = repo_tree if authoritative else combine(
            common_snapshot(repo, wiki, master, wiki_head), remote_tree, repo_tree)
        if target == remote_tree:
            print("Wiki already current; no commit.")
            return
        write_tree(wiki, "", remote_tree, target)
        git(wiki, "commit", "-m", f"docs(wiki): publish Gondwana {master}\n\nGondwana-Source: {master}")
        # Normal fast-forward push rejects a concurrent Wiki edit, without rewriting history.
        git(wiki, "push", "origin", "HEAD:refs/heads/master")
        print(f"Published documentation from {master}.")
        return

    pr = open_pr(repository)
    if pr:
        git(repo, "fetch", "origin", f"refs/heads/{BRANCH}")
        previous = git(repo, "rev-parse", "FETCH_HEAD").decode().strip()
        git(repo, "checkout", "--detach", previous)
        fork = git(repo, "merge-base", master, previous).decode().strip()
        changed = git(repo, "diff", "--name-only", "-z", fork, previous).decode().split("\0")
        if any(p and not p.startswith(PREFIX) and p != STATE for p in changed):
            raise RuntimeError("Automation branch contains unrelated edits; inspect its PR")
        git(repo, "merge", "--no-edit", "-m", "docs(wiki): incorporate current master", master)
    else:
        git(repo, "checkout", "--detach", master)
        # A merged/closed PR may leave the stable branch behind. Lease its exact old tip.
        refs = git(repo, "ls-remote", "origin", f"refs/heads/{BRANCH}").decode().split()
        previous = refs[0] if refs else ""

    repo_tree = snapshot(repo, "HEAD", PREFIX)
    target = remote_tree if authoritative or repo_tree == remote_tree else combine(
        common_snapshot(repo, wiki, "HEAD", wiki_head), repo_tree, remote_tree)
    if target == repo_tree:
        # Do not push a locally created master-merge commit merely to refresh
        # the automation branch when no Wiki content needs importing.
        if pr:
            ensure_pr(repository, pr)
        print("Repository already current; no commit or PR update.")
        return
    if target != repo_tree:
        write_tree(repo, PREFIX, repo_tree, target)
        # Store outside docs/** so existing CI does not skip Wiki-content PRs.
        # Record the master revision too: a manual restore from an unchanged
        # Wiki must still change the checkpoint and trigger existing PR CI.
        state_bytes = (json.dumps({"wiki_commit": wiki_head, "repo_commit": master},
                                  indent=2) + "\n").encode()
        state_sha = run("git", "-C", str(repo), "hash-object", "-w", "--stdin",
                        data=state_bytes).stdout.decode().strip()
        git(repo, "update-index", "--add", "--cacheinfo", "100644", state_sha, STATE)
        git(repo, "commit", "-m", f"docs(wiki): import Wiki {wiki_head} [skip release notes]")
    head = git(repo, "rev-parse", "HEAD").decode().strip()
    if head == master and pr is None:
        print("Repository already current; no commit or PR.")
        return
    if head != previous:
        if git(repo, "ls-remote", "origin", "refs/heads/master").decode().split()[0] != master:
            raise RuntimeError("master moved during import; rerun synchronization")
        # Only the dedicated branch is writable. The lease rejects other writers.
        git(repo, "push", f"--force-with-lease=refs/heads/{BRANCH}:{previous}",
            "origin", f"HEAD:refs/heads/{BRANCH}")
    ensure_pr(repository, pr)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("direction", choices=("repo-to-wiki", "wiki-to-repo"))
    parser.add_argument("--authoritative", action="store_true")
    args = parser.parse_args()
    repository = os.environ["GITHUB_REPOSITORY"]
    if repository != "Isthimius/Gondwana":
        raise RuntimeError("This workflow is restricted to Isthimius/Gondwana")
    if not os.environ.get("WIKI_SYNC_TOKEN"):
        raise RuntimeError("Set WIKI_SYNC_TOKEN with Contents and Pull requests write access")
    os.environ["GH_TOKEN"] = os.environ["WIKI_SYNC_TOKEN"]
    os.environ["GIT_TERMINAL_PROMPT"] = "0"
    with tempfile.TemporaryDirectory(prefix="gondwana-wiki-") as temp:
        root = Path(temp)
        askpass = root / "askpass.py"
        askpass.write_text("#!/usr/bin/env python3\nimport os, sys\n"
                           "print('x-access-token' if 'username' in sys.argv[1].lower() "
                           "else os.environ['WIKI_SYNC_TOKEN'])\n")
        askpass.chmod(0o700)
        os.environ["GIT_ASKPASS"] = str(askpass)
        repo, wiki = root / "repo", root / "wiki"
        for folder, suffix in ((repo, ""), (wiki, ".wiki")):
            run("git", "-c", "credential.helper=", "clone", "--no-tags",
                f"https://github.com/{repository}{suffix}.git", str(folder))
            git(folder, "config", "credential.helper", "")
            git(folder, "config", "user.name", "github-actions[bot]")
            git(folder, "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com")
        synchronize(repo, wiki, args.direction, args.authoritative, repository)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, KeyError, ValueError) as error:
        print(f"::error::{error}")
        raise SystemExit(1)
