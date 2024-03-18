﻿namespace CodeWF.Configuration;

public class NotificationSettings : IBlogSettings
{
    [Display(Name = "Enable email sending")]
    public bool EnableEmailSending { get; set; }

    [Display(Name = "Send email on comment reply")]
    public bool SendEmailOnCommentReply { get; set; }

    [Display(Name = "Send email on new comment")]
    public bool SendEmailOnNewComment { get; set; }

    [Required]
    [Display(Name = "Display name")]
    [MaxLength(64)]
    public string EmailDisplayName { get; set; }

    [JsonIgnore]
    public static NotificationSettings DefaultValue => new()
    {
        EnableEmailSending = false,
        SendEmailOnCommentReply = false,
        SendEmailOnNewComment = false,
        EmailDisplayName = "CodeWF"
    };
}