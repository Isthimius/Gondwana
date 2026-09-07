#!/usr/bin/env bash
set -euo pipefail
source_dir=$(realpath "$1")
version=$2
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
pages_dir=$(mktemp -d)
trap 'git worktree remove --force "$pages_dir" 2>/dev/null || rm -rf "$pages_dir"' EXIT
git config user.name github-actions
git config user.email actions@github.com
# Ordinary pushes plus retry preserve concurrent updates from any Pages writer.
for attempt in 1 2 3 4 5; do
  git ls-remote --heads origin gh-pages > "$pages_dir.refs"
  if [ -s "$pages_dir.refs" ]; then
    git fetch origin refs/heads/gh-pages
    git worktree add --detach "$pages_dir" FETCH_HEAD
  else
    git worktree add --detach "$pages_dir" HEAD
    git -C "$pages_dir" checkout --orphan api-pages-initial
    git -C "$pages_dir" rm -rf .
  fi
  rm -f "$pages_dir.refs"
  python3 "$script_dir/publish.py" --site "$pages_dir" --source "$source_dir" --version "$version"
  git -C "$pages_dir" add -- api
  if git -C "$pages_dir" diff --cached --quiet; then
    exit 0
  fi
  git -C "$pages_dir" commit -m "Publish API documentation: $version"
  if git -C "$pages_dir" push origin HEAD:refs/heads/gh-pages; then
    exit 0
  fi
  git worktree remove --force "$pages_dir"
done
echo 'Unable to publish API docs after five attempts' >&2
exit 1
