using LWBridge.Desktop.Checks;

// Small delivery suite. The research branch retains the wider campaign suites.
await GameRootSelectChecks.RunAsync();
await HomeR1AdoptionChecks.RunAsync();
await OrderedProfileReconcileChecks.RunAsync();
await MapStoredDeliveryChecks.RunAsync();
Console.WriteLine("HOME_FEATURE_DELIVERY_CHECKS_OK");
