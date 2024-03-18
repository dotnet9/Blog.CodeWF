﻿namespace CodeWF.Pingback;

public enum PingbackResponse
{
    Success,
    GenericError,
    InvalidPingRequest,
    Error32TargetUriNotExist,
    Error48PingbackAlreadyRegistered,
    Error17SourceNotContainTargetUri,
    SpamDetectedFakeNotFound
}