#pragma warning disable CS8600, CS8601, CS8602, CS8619, CS8629
using System;

namespace LWControl.Desktop;

internal sealed class ShieldHotkeyStateMachine
{
	private static readonly TimeSpan RestoreRetryDelay = TimeSpan.FromMilliseconds(250L);

	private const int MaximumRestoreAttempts = 3;

	private bool previousEligible;

	private bool pressActive;

	private bool downAttemptedForPress;

	private bool downInFlight;

	private bool downAcknowledged;

	private bool readInFlight;

	private bool readAcknowledged;

	private bool restoreInFlight;

	private bool remoteMayBeHeld;

	private int restoreAttempts;

	private DateTimeOffset nextRestoreAttemptAt = DateTimeOffset.MinValue;

	public ShieldHotkeyAction Observe(bool eligible, bool keyDown, DateTimeOffset now)
	{
		bool flag = previousEligible && !eligible;
		previousEligible = eligible;
		if (!(eligible & keyDown))
		{
			pressActive = false;
			downAttemptedForPress = false;
			downAcknowledged = false;
			readAcknowledged = false;
			if ((flag || remoteMayBeHeld || downInFlight || readInFlight) && !restoreInFlight && restoreAttempts < 3 && now >= nextRestoreAttemptAt)
			{
				restoreInFlight = true;
				restoreAttempts++;
				return ShieldHotkeyAction.HotkeyUp;
			}
			return ShieldHotkeyAction.None;
		}
		if (!pressActive)
		{
			pressActive = true;
			downAttemptedForPress = false;
			downAcknowledged = false;
			readAcknowledged = false;
			restoreAttempts = 0;
			nextRestoreAttemptAt = DateTimeOffset.MinValue;
			remoteMayBeHeld = true;
		}
		if (!restoreInFlight && !downInFlight && !downAcknowledged && !downAttemptedForPress)
		{
			downAttemptedForPress = true;
			downInFlight = true;
			remoteMayBeHeld = true;
			return ShieldHotkeyAction.HotkeyDown;
		}
		if (restoreInFlight || downInFlight || !downAcknowledged)
		{
			return ShieldHotkeyAction.None;
		}
		if (!readInFlight && !readAcknowledged)
		{
			readInFlight = true;
			return ShieldHotkeyAction.Read;
		}
		return ShieldHotkeyAction.None;
	}

	public ShieldHotkeyAction ForceRestore(DateTimeOffset now)
	{
		previousEligible = false;
		pressActive = false;
		downAttemptedForPress = false;
		downAcknowledged = false;
		readAcknowledged = false;
		remoteMayBeHeld = true;
		if (restoreInFlight || restoreAttempts >= 3 || now < nextRestoreAttemptAt)
		{
			return ShieldHotkeyAction.None;
		}
		restoreInFlight = true;
		restoreAttempts++;
		return ShieldHotkeyAction.HotkeyUp;
	}

	public void Complete(ShieldHotkeyAction action, bool succeeded, DateTimeOffset now)
	{
		switch (action)
		{
		case ShieldHotkeyAction.HotkeyDown:
			downInFlight = false;
			downAcknowledged = succeeded && pressActive && !restoreInFlight;
			break;
		case ShieldHotkeyAction.Read:
			readInFlight = false;
			readAcknowledged = true;
			break;
		case ShieldHotkeyAction.HotkeyUp:
			restoreInFlight = false;
			if (succeeded)
			{
				remoteMayBeHeld = false;
				restoreAttempts = 0;
				nextRestoreAttemptAt = DateTimeOffset.MinValue;
			}
			else
			{
				remoteMayBeHeld = true;
				nextRestoreAttemptAt = now + RestoreRetryDelay;
			}
			break;
		}
	}

	public void Reset()
	{
		previousEligible = false;
		pressActive = false;
		downAttemptedForPress = false;
		downInFlight = false;
		downAcknowledged = false;
		readInFlight = false;
		readAcknowledged = false;
		restoreInFlight = false;
		remoteMayBeHeld = false;
		restoreAttempts = 0;
		nextRestoreAttemptAt = DateTimeOffset.MinValue;
	}
}
