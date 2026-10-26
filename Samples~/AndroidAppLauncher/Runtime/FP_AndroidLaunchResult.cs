// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.
namespace FuzzPhyte.Utility
{
    public enum FP_AndroidLaunchStatus
    {
        LaunchFailed = 0,
        Success,
        InvalidPackageName,
        UnsupportedPlatform,
        ApplicationUnavailable,
        InvalidPayload
    }

    public readonly struct FP_AndroidLaunchResult
    {
        public FP_AndroidLaunchStatus Status { get; }
        public string Message { get; }
        public bool Success => Status == FP_AndroidLaunchStatus.Success;

        public FP_AndroidLaunchResult(FP_AndroidLaunchStatus status, string message)
        {
            Status = status;
            Message = message;
        }
    }
}
