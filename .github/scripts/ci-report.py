#!/usr/bin/env python3
"""Builds the markdown report of a CI run: version, test results and coverage.

Usage: ci-report.py <gitversion.json> <test-results.trx> <SummaryGithub.md>

It prints markdown to stdout. Any input that does not exist is reported as missing instead of failing,
so the report is still useful when an earlier step broke.
"""

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

TRX_NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
MAX_FAILURES_SHOWN = 20
MAX_COVERAGE_CHARS = 50_000  # a pull request comment holds at most 65,536 characters


def version_section(path: Path) -> str:
    if not path.exists():
        return "### Version\n\n_GitVersion did not produce a result._\n"

    data = json.loads(path.read_text())
    rows = [
        ("FullSemVer", data.get("FullSemVer")),
        ("SemVer", data.get("SemVer")),
        ("Branch", data.get("BranchName")),
        ("Commits since the last version source", data.get("CommitsSinceVersionSource")),
    ]
    table = "\n".join(f"| {name} | `{value}` |" for name, value in rows)
    return f"### Version\n\n| | |\n|---|---|\n{table}\n"


def tests_section(path: Path) -> str:
    if not path.exists():
        return "### Tests\n\n_No test results were produced._\n"

    root = ET.parse(path).getroot()
    counters = root.find("t:ResultSummary/t:Counters", TRX_NAMESPACE)
    total = int(counters.get("total", 0))
    passed = int(counters.get("passed", 0))
    failed = int(counters.get("failed", 0))
    skipped = total - passed - failed

    icon = "✅" if failed == 0 and total > 0 else "❌"
    text = f"### Tests {icon}\n\n| Total | Passed | Failed | Skipped |\n|---:|---:|---:|---:|\n"
    text += f"| {total} | {passed} | {failed} | {skipped} |\n"

    failures = [
        result
        for result in root.findall("t:Results/t:UnitTestResult", TRX_NAMESPACE)
        if result.get("outcome") == "Failed"
    ]
    if failures:
        text += "\n**Failed tests**\n\n"
        for result in failures[:MAX_FAILURES_SHOWN]:
            message = result.find("t:Output/t:ErrorInfo/t:Message", TRX_NAMESPACE)
            first_line = (message.text or "").strip().splitlines()[0] if message is not None and message.text else ""
            text += f"- `{result.get('testName')}`: {first_line}\n"
        if len(failures) > MAX_FAILURES_SHOWN:
            text += f"- … and {len(failures) - MAX_FAILURES_SHOWN} more\n"

    return text


def coverage_section(path: Path) -> str:
    if not path.exists():
        return "### Coverage\n\n_No coverage report was produced._\n"

    body = path.read_text().strip()
    if len(body) > MAX_COVERAGE_CHARS:
        body = body[:MAX_COVERAGE_CHARS] + "\n\n_(report cut: see the run summary for the rest)_"

    # ReportGenerator starts with its own "# Summary" heading; keep ours consistent.
    body = body.replace("# Summary", "### Coverage", 1)
    return body + "\n"


def main() -> None:
    version, trx, coverage = (Path(argument) for argument in sys.argv[1:4])
    print("\n".join([version_section(version), tests_section(trx), coverage_section(coverage)]))


if __name__ == "__main__":
    main()
