using LWBridge.Desktop.Checks;

// Small delivery suite. The research branch retains the wider campaign suites.
await GameRootSelectChecks.RunAsync();
await OrderedProfileReconcileChecks.RunAsync();
Console.WriteLine("HOME_FEATURE_DELIVERY_CHECKS_OK");
