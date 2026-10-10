using LWBridge.Desktop.Checks;

// Small delivery suite. The research branch retains the wider campaign suites.
await GameRootSelectChecks.RunAsync();
await HomeAutomationParityChecks.RunAsync();
await HomeR1AdoptionChecks.RunAsync();
await HomeR2RecoveryChecks.RunAsync();
await OrderedProfileReconcileChecks.RunAsync();
HomeR4TransportChecks.Run();
await MapStoredDeliveryChecks.RunAsync();
Console.WriteLine("HOME_FEATURE_DELIVERY_CHECKS_OK");
