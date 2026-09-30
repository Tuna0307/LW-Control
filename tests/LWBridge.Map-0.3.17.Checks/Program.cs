using LWBridge.Map317.Checks;

StoreChecks.Run();
QueryChecks.Run();
ExportChecks.Run();
ScanStateChecks.Run();
ControlPlaneChecks.Run();
Console.WriteLine("LWB317_MAP_CHECKS_OK");
