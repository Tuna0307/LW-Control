using LWBridge.Desktop.Checks;

if ((args.Length == 3 || args.Length == 4) && args[0] == "--map005-audit")
{
    await MapManualScanPublishedAudit.RunAsync(args[1], args[2],
        args.Length == 4 ? args[3] : null);
    return;
}

// Small delivery suite. The research branch retains the wider campaign suites.
await GameRootSelectChecks.RunAsync();
await HomeAutomationParityChecks.RunAsync();
await HomeFunctionalProfileCrudChecks.RunAsync();
await HomeR1AdoptionChecks.RunAsync();
await HomeR2RecoveryChecks.RunAsync();
await OrderedProfileReconcileChecks.RunAsync();
HomeR4TransportChecks.Run();
await MapStoredDeliveryChecks.RunAsync();
await MapManualScanDeliveryChecks.RunAsync();
Console.WriteLine("HOME_FEATURE_DELIVERY_CHECKS_OK");
