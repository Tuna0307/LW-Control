"""Preseed a *new*, disabled native profile for safe HOME-004 UI preflight.

Creates only an explicitly task-local isolated root. Never edits an existing
profile, installed game, WebView store or application owner data. Original
Auto Launch in WebView is ON by default and must be turned OFF in the actual
Home UI before this profile is ever enabled.
"""

from __future__ import annotations

import argparse
import json
import sqlite3
import time
from pathlib import Path


BASE = (Path(__file__).resolve().parents[1] / "artifacts" / "home-004").resolve()
PROFILE = "home-004-disabled-a"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("target", type=Path)
    arguments = parser.parse_args()
    target = arguments.target.resolve()
    if target == BASE or BASE not in target.parents:
        parser.error("target must be a fresh descendant of artifacts/home-004")
    if target.exists():
        parser.error("refusing to overwrite any existing isolated root")
    target.mkdir(parents=True)
    config = {
        "schemaVersion": 1,
        "owner": "LWBridgeRebuild",
        "profileId": PROFILE,
        "gameRoot": None,
        "autoLaunchGame": False,
        "autoReconnect": False,
        "gameDesiredRunning": False,
        "serverJumpHistory": [],
    }
    (target / "config.json").write_text(json.dumps(config, indent=2), encoding="utf-8")
    with sqlite3.connect(target / "controller.db") as db:
        db.executescript("""
            CREATE TABLE controller_state(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE profiles (
                id TEXT PRIMARY KEY, display_name TEXT NOT NULL, role_name TEXT,
                server_id TEXT, game_uid TEXT UNIQUE, note TEXT NOT NULL DEFAULT '',
                display_order INTEGER NOT NULL, enabled INTEGER NOT NULL DEFAULT 1,
                locked_reason TEXT, is_primary INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL,
                last_launched_at INTEGER
            );
            CREATE UNIQUE INDEX idx_profiles_primary
                ON profiles(is_primary) WHERE is_primary = 1;
        """)
        stamp = int(time.time() * 1000)
        db.execute("""INSERT INTO profiles(id,display_name,note,display_order,enabled,
                         is_primary,created_at,updated_at)
                      VALUES (?,?,'',0,0,1,?,?)""", (PROFILE, "Home 004 Disabled", stamp, stamp))
        db.execute("INSERT INTO controller_state VALUES('selected_profile_id', ?)", (PROFILE,))
        db.commit()
    print(json.dumps({"isolatedRoot": str(target), "profileId": PROFILE,
                      "profileEnabled": False, "nativeMirrorAutoLaunch": False,
                      "webViewAutoLaunch": "UNSET; turn OFF inside Home before enabling"}))


if __name__ == "__main__":
    main()
