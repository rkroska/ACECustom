using System.Collections.Generic;

namespace ACE.Server.Managers.QuestBuilder
{
    public class QuestPackageDto
    {
        public string Package { get; set; }
        public string Description { get; set; }
        public int CooldownSeconds { get; set; } = 86400;
        public List<QuestStampDto> Stamps { get; set; } = new();
        public List<QuestItemDto> Items { get; set; } = new();
        public List<QuestActorDto> Actors { get; set; } = new();
        public List<QuestCreatureDto> Creatures { get; set; } = new();
    }

    public class QuestStampDto
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public int MinDelta { get; set; } = -1;
        public int MaxSolves { get; set; } = -1;
    }

    public class QuestItemDto
    {
        public uint Wcid { get; set; }
        public string Name { get; set; }
        public string LongDesc { get; set; }
        public uint? CloneFromWcid { get; set; }
    }

    public class QuestActorDto
    {
        public uint Wcid { get; set; }
        public string Name { get; set; }
        public uint? CloneFromWcid { get; set; }
        /// <summary>questGiver | landscapePickup — set by journey sync for export labels.</summary>
        public string Role { get; set; }
        public List<QuestFlowDto> Flows { get; set; } = new();
    }

    public class QuestFlowDto
    {
        public string Trigger { get; set; }
        public uint? GiveWcid { get; set; }
        public List<QuestStepDto> Steps { get; set; } = new();
    }

    public class QuestStepDto
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public string Stamp { get; set; }
        public uint? Wcid { get; set; }
        public int? Stack { get; set; }
        public double Delay { get; set; }
        public string Motion { get; set; }
        public QuestStepBranchesDto Branches { get; set; }
    }

    public class QuestStepBranchesDto
    {
        public List<QuestStepDto> OnCooldown { get; set; } = new();
        public List<QuestStepDto> CanComplete { get; set; } = new();
    }

    public class QuestCreatureDto
    {
        public uint Wcid { get; set; }
        public string Name { get; set; }
        public uint? TemplateWcid { get; set; }
        public bool PatchExisting { get; set; }
        public uint DropItemWcid { get; set; }
        public int DropStack { get; set; } = 1;
    }

    public class QuestValidationIssueDto
    {
        public string Severity { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
    }

    public class QuestValidationResultDto
    {
        public bool Ok { get; set; }
        public List<QuestValidationIssueDto> Issues { get; set; } = new();
    }

    public class QuestTemplateInfoDto
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
    }

    public class NextWcidResultDto
    {
        public uint Wcid { get; set; }
        public uint RangeStart { get; set; }
        public uint RangeEnd { get; set; }
    }

    public class QuestExportFileDto
    {
        public string FileName { get; set; }
        public string Content { get; set; }
    }

    public class QuestExportResultDto
    {
        public string PackageName { get; set; }
        public List<QuestExportFileDto> Files { get; set; } = new();
        public string Readme { get; set; }
    }

    public class CreatureSearchResultDto
    {
        public uint Wcid { get; set; }
        public string Name { get; set; }
        public string ClassName { get; set; }
        public string WeenieType { get; set; }
    }
}
