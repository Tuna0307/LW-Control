"""Build the remaining-tabs test ZIP using the qualified programme integrity gates.

Requires a fresh publish from the exact clean, pushed remaining-tabs branch.
This does not launch the app, accept the changes, merge or release anything.
"""
import lwtool_full_improvement_package as programme

programme.BRANCH = "codex/lwtool-remaining-tabs"

if __name__ == "__main__":
    programme.main()
