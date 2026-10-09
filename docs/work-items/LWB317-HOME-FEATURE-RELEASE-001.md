# Home feature delivery 001

Owner requested a runnable feature-by-feature delivery for main, rather than more broad research campaigns. Lead works solo; the stopped worker remains stopped.

Deliver a clean candidate branch based on main with production source, runtime helpers, reproducible build, small focused checks and explicit feature status. Preserve the research branch/archive. No game launch or scan is assigned here; inspect the packaged Home UI using isolated app data, preserve actual failures and do not claim complete original Home/Map parity.

Initial target was the folder picker. Installed-game autodetection hides that control in the normal Home state, as intended by the recovered renderer. Native root selection/status/persistence can be checked through the existing actual-backend tests; actual OS-dialog interaction remains unverified in this delivery. The visible delivered slice is Home startup configuration: immediate Auto Launch preference, native preference persistence, registry startup admission, normal mounted Home and reload.

Concrete defects discovered: isolated capture dereferenced a null production root; isolated probes used the shared browser cache; backend dispatch bypassed registry reconciliation. Fix and distinguish them through the actual packaged capture and backend dispatch. Keep every other lifecycle/provider limitation explicit. Open a reviewable main PR and provide a runnable local package. Do not promote incomplete functions to A-to-A acceptance merely because the candidate builds.
