"""Local bare-Git integration tests; no credentials or GitHub mutations."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import sync


class MergeTests(unittest.TestCase):
    def test_independent_changes_and_deletions(self):
        self.assertEqual(sync.combine({"a": 1, "b": 2}, {"a": 3, "b": 2},
                                      {"a": 1, "c": 4}), {"a": 3, "c": 4})

    def test_conflicts(self):
        for ours, theirs in ((2, 3), (None, 3), (2, None)):
            with self.subTest(ours=ours, theirs=theirs), self.assertRaises(RuntimeError):
                sync.combine({"a": 1}, {} if ours is None else {"a": ours},
                             {} if theirs is None else {"a": theirs})

    def test_identical_edits_are_noop(self):
        self.assertEqual(sync.combine({"a": 1}, {"a": 2}, {"a": 2}), {"a": 2})


class ApiTests(unittest.TestCase):
    def test_create_title_and_enable_without_direct_merge(self):
        pr = {"number": 7, "node_id": "PR_test", "html_url": "https://example.invalid/pr/7"}
        with patch.object(sync, "api", side_effect=[pr, {"allow_squash_merge": True,
                              "allow_auto_merge": True}, {"data": {}}]) as api:
            sync.ensure_pr("Isthimius/Gondwana", None)
        create, settings, enable = api.call_args_list
        self.assertTrue(create.args[2]["title"].startswith("docs(wiki):"))
        self.assertEqual(create.args[2]["base"], "master")
        self.assertEqual(create.args[2]["head"], sync.BRANCH)
        self.assertIn("enablePullRequestAutoMerge", enable.args[2]["query"])
        self.assertNotIn("mergePullRequest", enable.args[2]["query"])

    def test_existing_pr_is_not_updated_when_current(self):
        with patch.object(sync, "api") as api:
            sync.ensure_pr("Isthimius/Gondwana", {
                "title": sync.TITLE, "auto_merge": {"merge_method": "squash"},
                "html_url": "https://example.invalid/pr/7"})
        api.assert_not_called()

    def test_duplicate_prs_fail(self):
        with patch.object(sync, "api", return_value=[{}, {}]), self.assertRaises(RuntimeError):
            sync.open_pr("Isthimius/Gondwana")


class GitTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.wiki = self.init("wiki")
        self.commit(self.wiki, {"Home.md": b"home\n", "Old.md": b"old\n",
                               "images/a.png": bytes(range(256)),
                               ".gitignore": b"*.ignored\n", "asset.ignored": b"keep"})
        self.initial = self.sha(self.wiki)
        self.repo = self.init("repo")
        self.commit(self.repo, {sync.PREFIX + p: value[1]
                               for p, value in sync.snapshot(self.wiki, "HEAD").items()} | {
            sync.PREFIX + sync.README: b"Infrastructure only\n",
            sync.STATE: (json.dumps({"wiki_commit": self.initial}) + "\n").encode()})
        self.repo_remote = self.root / "repo.git"
        self.wiki_remote = self.root / "wiki.git"
        for local, remote in ((self.repo, self.repo_remote), (self.wiki, self.wiki_remote)):
            sync.run("git", "clone", "--bare", str(local), str(remote))
            sync.git(local, "remote", "add", "origin", str(remote))
        self.pr = None
        self.pr_calls = 0

    def init(self, name):
        folder = self.root / name
        sync.run("git", "init", "-b", "master", str(folder))
        self.identity(folder)
        return folder

    def identity(self, folder):
        sync.git(folder, "config", "user.name", "Test")
        sync.git(folder, "config", "user.email", "test@example.invalid")

    def sha(self, repo, ref="HEAD"):
        return sync.git(repo, "rev-parse", ref).decode().strip()

    def commit(self, repo, changes, message="test: edit"):
        for path, content in changes.items():
            target = repo / path
            if content is None:
                target.unlink()
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(content)
        sync.git(repo, "add", "-f", "--all")
        sync.git(repo, "commit", "-m", message)
        return self.sha(repo)

    def edit(self, side, changes):
        folder = self.repo if side == "repo" else self.wiki
        sync.git(folder, "pull", "--ff-only", "origin", "master")
        result = self.commit(folder, changes)
        sync.git(folder, "push", "origin", "master")
        return result

    def execute(self, direction, authoritative=False):
        with tempfile.TemporaryDirectory(dir=self.root) as temp:
            repo, wiki = Path(temp) / "repo", Path(temp) / "wiki"
            for folder, remote in ((repo, self.repo_remote), (wiki, self.wiki_remote)):
                sync.run("git", "clone", str(remote), str(folder))
                sync.git(folder, "config", "user.name", "github-actions[bot]")
                sync.git(folder, "config", "user.email", sync.BOT_EMAIL)
            def ensure(repository, pr):
                self.pr_calls += 1
                self.pr = {"number": 1}
            with patch.object(sync, "open_pr", return_value=self.pr), \
                    patch.object(sync, "ensure_pr", side_effect=ensure):
                sync.synchronize(repo, wiki, direction, authoritative, "Isthimius/Gondwana")

    def tree(self, side, ref="master"):
        remote = self.repo_remote if side == "repo" else self.wiki_remote
        return sync.snapshot(remote, ref, sync.PREFIX if side == "repo" else "")

    def merge_pr(self):
        sync.git(self.repo, "fetch", "origin", sync.BRANCH)
        sync.git(self.repo, "merge", "--squash", "FETCH_HEAD")
        sync.git(self.repo, "commit", "-m", sync.TITLE)
        sync.git(self.repo, "push", "origin", "master")
        self.pr = None

    def test_noop_both_directions(self):
        wiki = self.sha(self.wiki_remote)
        repo = self.sha(self.repo_remote)
        self.execute("repo-to-wiki")
        self.execute("wiki-to-repo")
        self.assertEqual(wiki, self.sha(self.wiki_remote))
        self.assertEqual(repo, self.sha(self.repo_remote))
        self.assertEqual(self.pr_calls, 0)

    def test_import_delete_rename_binary_and_readme(self):
        master = self.sha(self.repo_remote)
        self.edit("wiki", {"Old.md": None, "New.md": b"old\n",
                           "images/a.png": b"\x00new\xff", "README.md": b"must not import"})
        self.execute("wiki-to-repo")
        self.assertEqual(self.tree("repo", sync.BRANCH), self.tree("wiki"))
        self.assertEqual(master, self.sha(self.repo_remote))
        self.assertEqual(sync.git(self.repo_remote, "show", f"{sync.BRANCH}:{sync.PREFIX}README.md"),
                         b"Infrastructure only\n")
        first = self.sha(self.repo_remote, sync.BRANCH)
        self.execute("wiki-to-repo")
        self.assertEqual(first, self.sha(self.repo_remote, sync.BRANCH))

    def test_reuses_branch_and_pr(self):
        self.edit("wiki", {"Home.md": b"first"})
        self.execute("wiki-to-repo")
        first = self.sha(self.repo_remote, sync.BRANCH)
        self.edit("wiki", {"Home.md": b"second"})
        self.execute("wiki-to-repo")
        self.assertTrue(sync.ancestor(self.repo_remote, first, sync.BRANCH))
        refs = sync.git(self.repo_remote, "for-each-ref", "--format=%(refname)").decode().splitlines()
        self.assertEqual(set(refs), {"refs/heads/master", "refs/heads/" + sync.BRANCH})

    def test_noop_does_not_update_branch_for_unrelated_master_change(self):
        self.edit("wiki", {"Home.md": b"wiki"})
        self.execute("wiki-to-repo")
        first = self.sha(self.repo_remote, sync.BRANCH)
        self.edit("repo", {"code.txt": b"unrelated"})
        self.execute("wiki-to-repo")
        self.assertEqual(first, self.sha(self.repo_remote, sync.BRANCH))

    def test_publish_rename_and_delete(self):
        self.edit("repo", {sync.PREFIX + "Old.md": None, sync.PREFIX + "Renamed.md": b"old\n"})
        self.execute("repo-to-wiki")
        self.assertEqual(self.tree("wiki"), self.tree("repo"))

    def test_publish_preserves_unimported_wiki_edit(self):
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo"})
        self.edit("wiki", {"New.md": b"wiki"})
        self.execute("repo-to-wiki")
        self.assertEqual(self.tree("wiki")["New.md"][1], b"wiki")
        self.assertNotIn("README.md", sync.git(self.wiki_remote, "ls-tree", "--name-only", "HEAD").decode())
        self.execute("wiki-to-repo")
        self.assertEqual(self.tree("repo", sync.BRANCH), self.tree("wiki"))

    def test_wiki_edit_after_publish_uses_published_base(self):
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo"})
        self.execute("repo-to-wiki")
        self.edit("wiki", {"Home.md": b"later wiki"})
        self.execute("wiki-to-repo")
        self.assertEqual(self.tree("repo", sync.BRANCH)["Home.md"][1], b"later wiki")

    def test_conflict_changes_neither_remote(self):
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo"})
        self.edit("wiki", {"Home.md": b"wiki"})
        heads = self.sha(self.repo_remote), self.sha(self.wiki_remote)
        for direction in ("repo-to-wiki", "wiki-to-repo"):
            with self.assertRaisesRegex(RuntimeError, "Independent edits"):
                self.execute(direction)
        self.assertEqual(heads, (self.sha(self.repo_remote), self.sha(self.wiki_remote)))

    def test_manual_both_directions(self):
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo"})
        self.edit("wiki", {"Home.md": b"wiki", "Old.md": None})
        self.execute("wiki-to-repo", True)
        self.assertEqual(self.tree("repo", sync.BRANCH), self.tree("wiki"))
        self.execute("repo-to-wiki", True)
        self.assertEqual(self.tree("repo"), self.tree("wiki"))

    def test_manual_restore_unchanged_wiki_updates_checkpoint_for_ci(self):
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo"})
        self.execute("wiki-to-repo", True)
        changed = sync.git(self.repo_remote, "diff", "--name-only", "master", sync.BRANCH).decode()
        self.assertIn(sync.STATE, changed)
        checkpoint = json.loads(sync.git(self.repo_remote, "show", f"{sync.BRANCH}:{sync.STATE}"))
        self.assertEqual(checkpoint["repo_commit"], self.sha(self.repo_remote))

    def test_import_merge_publish_does_not_loop(self):
        self.edit("wiki", {"Home.md": b"wiki"})
        self.execute("wiki-to-repo")
        self.merge_pr()
        wiki = self.sha(self.wiki_remote)
        self.execute("repo-to-wiki")
        self.execute("wiki-to-repo")
        self.assertEqual(wiki, self.sha(self.wiki_remote))
        self.assertIsNone(self.pr)

    def test_weekly_recovers_both_sides_and_subsequent_edit(self):
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo"})
        self.edit("wiki", {"Old.md": None, "Renamed.md": b"old\n"})
        self.execute("wiki-to-repo")
        self.execute("repo-to-wiki")
        self.edit("wiki", {"Home.md": b"wiki after publish"})
        self.execute("wiki-to-repo")
        self.merge_pr()
        self.execute("repo-to-wiki")
        self.assertEqual(self.tree("repo"), self.tree("wiki"))

    def test_existing_pr_master_updates_and_same_file_conflict(self):
        self.edit("wiki", {"Home.md": b"wiki"})
        self.execute("wiki-to-repo")
        self.edit("repo", {"code.txt": b"unrelated"})
        self.edit("wiki", {"Other.md": b"another"})
        self.execute("wiki-to-repo")
        self.assertEqual(sync.git(self.repo_remote, "show", sync.BRANCH + ":code.txt"), b"unrelated")
        tip = self.sha(self.repo_remote, sync.BRANCH)
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo conflict"})
        with self.assertRaises(RuntimeError):
            self.execute("wiki-to-repo")
        self.assertEqual(tip, self.sha(self.repo_remote, sync.BRANCH))

    def test_existing_automation_branch_readme_edit_rejected(self):
        self.edit("wiki", {"Home.md": b"wiki"})
        self.execute("wiki-to-repo")
        sync.git(self.repo, "fetch", "origin", sync.BRANCH)
        sync.git(self.repo, "checkout", "--detach", "FETCH_HEAD")
        self.commit(self.repo, {sync.PREFIX + sync.README: b"unexpected infrastructure edit"})
        sync.git(self.repo, "push", "origin", f"HEAD:refs/heads/{sync.BRANCH}")
        tip = self.sha(self.repo_remote, sync.BRANCH)
        master = self.sha(self.repo_remote)
        wiki = self.sha(self.wiki_remote)
        for authoritative in (False, True):
            with self.subTest(authoritative=authoritative), self.assertRaisesRegex(
                    RuntimeError, "reserved README"):
                self.execute("wiki-to-repo", authoritative)
        self.assertEqual(tip, self.sha(self.repo_remote, sync.BRANCH))
        self.assertEqual(master, self.sha(self.repo_remote))
        self.assertEqual(wiki, self.sha(self.wiki_remote))

    def test_forged_trailer_cannot_hide_competing_edits(self):
        self.edit("repo", {sync.PREFIX + "Home.md": b"repo"})
        master = self.sha(self.repo_remote)
        sync.git(self.wiki, "pull", "--ff-only", "origin", "master")
        self.commit(self.wiki, {"Home.md": b"wiki"},
                    message=f"docs(wiki): publish Gondwana {master}\n\nGondwana-Source: {master}")
        sync.git(self.wiki, "push", "origin", "master")
        tip = self.sha(self.repo_remote)
        with self.assertRaisesRegex(RuntimeError, "Independent edits"):
            self.execute("wiki-to-repo")
        self.assertEqual(tip, self.sha(self.repo_remote))
        self.assertEqual(sync.git(self.repo_remote, "show", f"master:{sync.PREFIX}Home.md"), b"repo")

    def test_symlink_rejected(self):
        (self.wiki / "link.md").symlink_to("Home.md")
        self.commit(self.wiki, {})
        with self.assertRaisesRegex(RuntimeError, "Unsupported Wiki entry"):
            sync.snapshot(self.wiki, "HEAD")


if __name__ == "__main__":
    unittest.main()
