#!/usr/bin/env bash
#
# Regenerates the syntax diagram SVGs under docs/images/ from the EBNF railroad diagram
# sources under docs/diagrams/.  Requires Node.js/npm (only npx is actually invoked; nothing
# is installed globally).
#
# Usage: docs/generate-diagrams.sh
#
# For each docs/diagrams/<group>/<name>.mmd file, this produces two SVGs in
# docs/images/<group>/: <name>.svg (light theme) and <name>-dark.svg (dark theme), both with
# a transparent background, matching the <picture> light/dark pattern already used in the
# docs.  These are named/located the same way the old custom-built diagram tool's PNGs were
# (just with a .svg extension now), so no other changes are needed in the .md files that
# reference them after regenerating.
#
# The Mermaid railroad diagram feature (railroad-ebnf-beta) is still beta upstream, and we've
# already hit one real rendering bug in it (see the comment above CLI_VERSION below), so the
# mermaid-cli version is pinned rather than left to resolve to "whatever is latest" -- bump it
# deliberately, and re-check every generated diagram, rather than letting it drift.
set -euo pipefail

# Pinned because the railroad diagram feature is beta; this is the version everything here
# has actually been verified against.
CLI_VERSION="11.16.0"
CLI="@mermaid-js/mermaid-cli@${CLI_VERSION}"

if ! command -v npx > /dev/null 2>&1; then
    echo "npx (Node.js) is required to generate diagrams; see https://nodejs.org/." >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIAGRAMS_DIR="${SCRIPT_DIR}/diagrams"
IMAGES_DIR="${SCRIPT_DIR}/images"

for group_dir in "${DIAGRAMS_DIR}"/*/; do
    group="$(basename "${group_dir}")"
    out_dir="${IMAGES_DIR}/${group}"

    mkdir -p "${out_dir}"

    for source in "${group_dir}"*.mmd; do
        name="$(basename "${source}" .mmd)"

        echo "Generating ${group}/${name}..."

        npx --yes "${CLI}" -i "${source}" -o "${out_dir}/${name}.svg" \
            -t default -b transparent -q
        npx --yes "${CLI}" -i "${source}" -o "${out_dir}/${name}-dark.svg" \
            -t dark -b transparent -q
    done
done

echo "Done."
