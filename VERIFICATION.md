# Verification — 22 October 2026

The game patch recipes are unchanged from the locally verified 1.3.0 release: 8192 combinations built, older profiles preserved, apply/reapply/all-off/restore and manual-journal upgrade tested on isolated copies. The new UI maps each reordered entry by feature ID and bit, not by its displayed index. UI self-checks exercise all 13 default selections, reload/cloak and random/holster dependencies, and selecting every detail page. RU and EN output files are compared using isolated apply tests. Both GIFs contain 13 actual application frames at 1000 ms per frame. No working game files were modified for packaging or verification.

The public EXEs still need an end-to-end user test in VR.
