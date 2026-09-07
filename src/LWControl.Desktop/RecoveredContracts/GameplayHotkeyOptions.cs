#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
namespace LWControl.Desktop;

internal sealed record GameplayHotkeyOptions(bool QuickAttackQ = true, bool QuickAttackW = true, bool QuickAttackE = true, bool QuickAttackR = true, bool QuickRecallA = true, bool QuickRecallS = true, bool QuickRecallD = true, bool QuickRecallF = true, bool ShieldCountdownSpace = true, bool Shield8HoursF6 = true, bool Shield12HoursF7 = true, bool Shield24HoursF8 = true, bool EquipmentScheme1 = true, bool EquipmentScheme2 = true, bool EquipmentScheme3 = true, bool EquipmentScheme4 = true, bool RandomTeleportF9 = false)
{
	public bool IsQuickAttackEnabled(int team)
	{
		return team switch
		{
			1 => QuickAttackQ, 
			2 => QuickAttackW, 
			3 => QuickAttackE, 
			4 => QuickAttackR, 
			_ => false, 
		};
	}

	public bool IsQuickRecallEnabled(int team)
	{
		return team switch
		{
			1 => QuickRecallA, 
			2 => QuickRecallS, 
			3 => QuickRecallD, 
			4 => QuickRecallF, 
			_ => false, 
		};
	}

	public bool IsEquipmentSchemeEnabled(int slot)
	{
		return slot switch
		{
			1 => EquipmentScheme1, 
			2 => EquipmentScheme2, 
			3 => EquipmentScheme3, 
			4 => EquipmentScheme4, 
			_ => false, 
		};
	}
}
