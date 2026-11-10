import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import cumod


class ManagerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "game.dll").write_bytes(b"game")
        (self.root / "a.dll").write_bytes(b"a")
        (self.root / "b.dll").write_bytes(b"b")
        self.profile = {
            "schema": 1, "game": "casualties-unknown", "name": "test",
            "fingerprints": [{"source": "game.dll", "sha256": cumod.digest(b"game")}],
            "enabled": ["b", "a"], "components": [
                {"id": "a", "version": "1", "files": [{"source": "a.dll", "target": "BepInEx/plugins/a.dll"}]},
                {"id": "b", "version": "1", "requires": {"a": "1"}, "files": [{"source": "b.dll", "target": "BepInEx/plugins/b.dll"}]}
            ]}

    def lock(self):
        p = self.root / "profile.json"
        p.write_text(json.dumps(self.profile), encoding="utf-8")
        return cumod.make_lock(p)

    def bundle(self):
        lock = self.lock()
        p = self.root / "lock.json"
        cumod.write_new(p, lock)
        out = self.root / "bundle"
        cumod.stage(p, out)
        return out

    def test_dependency_order_and_roundtrip(self):
        self.assertEqual(self.lock()["order"], ["a", "b"])
        out = self.bundle()
        self.assertEqual(cumod.verify(out)["status"], "PASS")
        self.assertEqual((self.root / "game.dll").read_bytes(), b"game")

    def test_missing_dependency(self):
        self.profile["enabled"] = ["b"]
        with self.assertRaises(cumod.Invalid): self.lock()

    def test_version_mismatch(self):
        self.profile["components"][1]["requires"]["a"] = "2"
        with self.assertRaises(cumod.Invalid): self.lock()

    def test_cycle(self):
        self.profile["components"][0]["requires"] = {"b": "1"}
        with self.assertRaises(cumod.Invalid): self.lock()

    def test_declared_conflict(self):
        self.profile["components"][0]["conflicts"] = ["b"]
        with self.assertRaises(cumod.Invalid): self.lock()

    def test_case_and_directory_collisions(self):
        for path in ["bepinex/plugins/A.DLL", "BepInEx/plugins/a.dll/file"]:
            with self.subTest(path=path):
                self.profile["components"][1]["files"][0]["target"] = path
                with self.assertRaises(cumod.Invalid): self.lock()

    def test_bad_paths(self):
        for path in ["../escape", "C:/escape", "/escape", "a\\b", "a/../b", "CON.txt", "a.", "a//b", "a:stream"]:
            with self.subTest(path=path):
                with self.assertRaises(cumod.Invalid): cumod.relative(path)

    def test_archive_member(self):
        with zipfile.ZipFile(self.root / "loader.zip", "w") as z:
            z.writestr("core/a.dll", b"a")
        self.profile["components"][0]["files"][0].update(source="loader.zip", member="core/a.dll")
        self.assertEqual(cumod.verify(self.bundle())["files"], 2)

    def test_drift_rejected_before_output(self):
        p = self.root / "lock.json"
        cumod.write_new(p, self.lock())
        (self.root / "a.dll").write_bytes(b"changed")
        with self.assertRaises(cumod.Invalid): cumod.stage(p, self.root / "bundle")
        self.assertFalse((self.root / "bundle").exists())

    def test_game_drift(self):
        (self.root / "game.dll").write_bytes(b"new build")
        with self.assertRaises(cumod.Invalid): self.lock()

    def test_verify_missing_extra_and_modified(self):
        out = self.bundle()
        target = out / "payload/BepInEx/plugins/a.dll"
        target.unlink()
        with self.assertRaises(cumod.Invalid): cumod.verify(out)
        target.write_bytes(b"modified")
        with self.assertRaises(cumod.Invalid): cumod.verify(out)
        target.write_bytes(b"a")
        (out / "payload/extra").write_bytes(b"extra")
        with self.assertRaises(cumod.Invalid): cumod.verify(out)

    def test_incomplete_and_existing_output(self):
        out = self.bundle()
        with self.assertRaises(cumod.Invalid): cumod.stage(self.root / "lock.json", out)
        (out / "COMPLETE").write_text("incomplete")
        with self.assertRaises(cumod.Invalid): cumod.verify(out)

    def test_tampered_lock_traversal(self):
        lock = self.lock()
        lock["files"][0]["target"] = "../escape"
        with self.assertRaises(cumod.Invalid): cumod.checked_files(lock)


if __name__ == "__main__":
    unittest.main()
