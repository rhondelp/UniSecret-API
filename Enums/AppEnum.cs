namespace UniSecretApi.Enums;

public enum UniversityStatus
{
    Pending,
    Approved,
    Rejected
}

public enum UserRole
{
    Student,
    Admin,
    SuperAdmin
}

public enum UserStatus
{
    Active,
    Suspended,
    Banned
}

public enum ConfessionStatus
{
    Pending,
    Approved,
    Rejected,
    Scheduled
}

public enum ReportReason
{
    Bullying,
    HateSpeech,
    Spam,
    Nsfw,
    Harassment
}

public enum ReportStatus
{
    Pending,
    Reviewed,
    Dismissed,
    ActionTaken
}