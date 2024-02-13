using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Database.Models.Auth;

public partial class Account
{
    public uint AccountId { get; set; }

    public string AccountName { get; set; }

    /// <summary>
    /// base64 encoded version of the hashed passwords.  88 characters are needed to base64 encode SHA512 output.
    /// </summary>
    public string PasswordHash { get; set; }

    /// <summary>
    /// This is no longer used, except to indicate if bcrypt is being employed for migration purposes. Previously: base64 encoded version of the password salt.  512 byte salts (88 characters when base64 encoded) are recommend for SHA512.
    /// </summary>
    public string PasswordSalt { get; set; }

    public uint AccessLevel { get; set; }

    public string EmailAddress { get; set; }

    public DateTime CreateTime { get; set; }

    public byte[] CreateIP { get; set; }

    public string CreateIPNtoa { get; set; }

    public DateTime? LastLoginTime { get; set; }

    public byte[] LastLoginIP { get; set; }

    public string LastLoginIPNtoa { get; set; }

    public uint TotalTimesLoggedIn { get; set; }

    public DateTime? BannedTime { get; set; }

    public uint? BannedByAccountId { get; set; }

    public DateTime? BanExpireTime { get; set; }

    public string BanReason { get; set; }

    public virtual Accesslevel AccessLevelNavigation { get; set; }

    public List<AccountQuest> QuestCountCache { get; set; }

    public bool HasQuestBonusAndCompletion(string questName)
    {
        if (QuestCountCache == null)
        {
            QuestCountCache = this.GetAccountQuests();
        }
        if (QuestCountCache == null)
        {
            return false;
        }
        return QuestCountCache.Any(x => x.Quest == questName && x.NumTimesCompleted >= 1);
    }

    public bool HasQuestNotCompleted(string questName)
    {
        if (QuestCountCache == null)
        {
            QuestCountCache = this.GetAccountQuests();
        }
        if (QuestCountCache == null)
        {
            return false;
        }
        return QuestCountCache.Any(x => x.Quest == questName);
    }

    public long? CachedQuestBonusCount
    {
        get
        {
            if (QuestCountCache == null)
            {
                QuestCountCache = this.GetAccountQuests();
            }
            if (QuestCountCache == null)
            {
                return null;
            }
                
            return QuestCountCache.Count + QuestCountCache.Where(x=>x.NumTimesCompleted>=1).Count();
        }
    }

    public void UpdateAccountQuestsCacheByQuestName(string questName, uint numCompletions)
    {
        if (QuestCountCache == null)
        {
            QuestCountCache = this.GetAccountQuests();
        }
        if (QuestCountCache == null)
        {
            return;
        }
        var quest = QuestCountCache.FirstOrDefault(x => x.Quest == questName);
        if (quest == null)
        {
            quest = new AccountQuest
            {
                AccountId = this.AccountId,
                Quest = questName,
                NumTimesCompleted = numCompletions
            };
            QuestCountCache.Add(quest);
        }
        else
        {
            quest.NumTimesCompleted = numCompletions;
        }
    }
}
