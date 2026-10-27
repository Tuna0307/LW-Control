using LWBridge.Desktop.Checks;

if (args.Length == 1 && args[0] == "--startup")
{
    await Feature018RuntimeStartupChecks.RunAsync();
    return;
}

if (args.Length == 1 && args[0] == "--chat-rewards")
{
    await ChatRewardConfigChecks.RunAsync();
    return;
}

if (args.Length == 1 && args[0] == "--adapter-lease")
{
    GamePipeAdapterLeaseChecks.Run();
    return;
}

if (args.Length == 1 && args[0] == "--home-connection-stability")
{
    await HomeStaleAdoptionChecks.RunAsync();
    GamePipeAdapterLeaseChecks.Run();
    HomeR4TransportChecks.Run();
    Console.WriteLine("HOME_CONNECTION_STABILITY_CHANGED_PATH_CHECKS_OK");
    return;
}

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

// Delivery suite. Older campaign suites are in the archive/research-offline-controller tag.
await Feature018RuntimeStartupChecks.RunAsync();
await GameRootSelectChecks.RunAsync();
await HomeAutomationParityChecks.RunAsync();
await HomeFunctionalProfileCrudChecks.RunAsync();
await HomeR1AdoptionChecks.RunAsync();
await HomeStaleAdoptionChecks.RunAsync();
await HomeSecondaryRuntimeCompositionChecks.RunAsync();
GamePipeAdapterLeaseChecks.Run();
await Feature018HotkeyConfigChecks.RunAsync();
await AuditFindingsChecks.RunAsync();
await Feature018RetiredTasksChecks.RunAsync();
await Feature018OwnedGameExchangeChecks.RunAsync();
await Feature018AfkRuntimeChecks.RunAsync();
await Feature018AutomationChecks.RunAsync();
Feature018MissionConfigChecks.Run();
await ChatRewardConfigChecks.RunAsync();
await Feature018HotkeyCaptureChecks.RunAsync();
Feature018KeyProofCorrelationChecks.Run();
Feature018ProofOwnerGraceChecks.Run();
await Feature018EquipmentProviderChecks.RunAsync();
Feature018ChestMonitorChecks.Run();
await HomeR2RecoveryChecks.RunAsync();
await OrderedProfileReconcileChecks.RunAsync();
HomeR4TransportChecks.Run();
await MapStoredDeliveryChecks.RunAsync();
await MapManualScanDeliveryChecks.RunAsync();
await MapAutoScanTerminalChecks.RunAsync();
await MapAutoScanR1StopOrderingChecks.RunAsync();
await Map006ActionSafetyChecks.RunAsync();
await Map006AllKindScanChecks.RunAsync();
await Map006NavigationShareChecks.RunAsync();
await Map007ActionProviderChecks.RunAsync();
await Map014OwnerFixChecks.RunAsync();
await Map014GhostRewardFilterChecks.RunAsync();
Map014GhostRewardFlowChecks.Run();
Map014TruckImageWitnessChecks.Run();
await Map014OtherEventChecks.RunAsync();
Map014ZombieBossChecks.Run();
await Map014ConsecutiveEngineCleanupChecks.RunAsync();
await Map014TerminalSourceValidationChecks.RunAsync();
Map014ResourceHydrationReceiptChecks.Run();
await Map014SharedAcquisitionChecks.RunAsync();
await Map014Map317SharedAdapterChecks.RunAsync();
await Map014AutoSharedOwnershipChecks.RunAsync();
Map014SharedSourceAdmissionChecks.Run();
Map014ResourceSourceCompletenessChecks.Run();
Map014ResourceNativeSourceChecks.Run();
Map014BoundedLensChecks.Run();
Map014NativeFootprintChecks.Run();
await Map014WholeWorldFailureChecks.RunAsync();
await Map015CameraFreeDriftChecks.RunAsync();
await Map016RailwayChecks.RunAsync();
Map008InstalledNameChecks.Run();
Console.WriteLine("HOME_FEATURE_DELIVERY_CHECKS_OK");
