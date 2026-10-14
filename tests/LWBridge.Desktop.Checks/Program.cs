using LWBridge.Desktop.Checks;

if (args.Length == 3 && args[0] == "--map008-installed-names")
{
    Map008InstalledNameChecks.CheckInstalled(args[1], args[2]);
    return;
}

if ((args.Length == 3 || args.Length == 4) && args[0] == "--map005-audit")
{
    await MapManualScanPublishedAudit.RunAsync(args[1], args[2],
        args.Length == 4 ? args[3] : null);
    return;
}
if (args.Length == 3 && args[0] == "--map006-audit")
{
    await Map006PublishedAudit.RunAsync(args[1], args[2]);
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
await MapAutoScanTerminalChecks.RunAsync();
await MapAutoScanR1StopOrderingChecks.RunAsync();
await Map006ActionSafetyChecks.RunAsync();
await Map006AllKindScanChecks.RunAsync();
await Map006DispatchCorrelationChecks.RunAsync();
await Map006NavigationShareChecks.RunAsync();
await Map006TruckCorrelationChecks.RunAsync();
await Map007ActionProviderChecks.RunAsync();
await Map007GhostCorrelationChecks.RunAsync();
await Map014OwnerFixChecks.RunAsync();
await Map014OtherEventChecks.RunAsync();
await Map014ConsecutiveEngineCleanupChecks.RunAsync();
await Map014SharedAcquisitionChecks.RunAsync();
await Map014Map317SharedAdapterChecks.RunAsync();
await Map014AutoSharedOwnershipChecks.RunAsync();
Map014SharedSourceAdmissionChecks.Run();
Map014ResourceSourceCompletenessChecks.Run();
Map014BoundedLensChecks.Run();
Map008InstalledNameChecks.Run();
Console.WriteLine("HOME_FEATURE_DELIVERY_CHECKS_OK");
