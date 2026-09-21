using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Dtos;
using Jarvis5.Entities;

namespace Jarvis5.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<SCIHRequest, RequestListItemDto>()
            .ForMember(d => d.CurrentStageName, opt => opt.MapFrom(s => SCIHStage.NameOf(s.CurrentStage)));

        CreateMap<SCIHRequest, RequestDetailDto>()
            .ForMember(d => d.CurrentStageName, opt => opt.MapFrom(s => SCIHStage.NameOf(s.CurrentStage)))
            .ForMember(d => d.PainPoints, opt => opt.MapFrom(s => JsonHelper.DeserializeList<PainPointDto>(s.PainPointsJson)))
            .ForMember(d => d.Attachments, opt => opt.MapFrom(s => JsonHelper.DeserializeList<AttachmentDto>(s.AttachmentsJson)))
            .ForMember(d => d.Meta, opt => opt.MapFrom(s => JsonHelper.DeserializeObjectOrDefault<RequestMetaDto>(s.MetaJson)))
            .ForMember(d => d.LatestHistory, opt => opt.Ignore());

        CreateMap<SCIHRequestHistory, RequestHistoryDto>()
            .ForMember(d => d.PreviousValue, opt => opt.MapFrom(s => JsonHelper.ParseOrNull(s.PreviousValue)))
            .ForMember(d => d.NewValue, opt => opt.MapFrom(s => JsonHelper.ParseOrNull(s.NewValue)));

        CreateMap<SCIHAttachment, Dtos.Attachment.AttachmentResponseDto>();

        // EA FMS mappings
        CreateMap<Jarvis5.Entities.EaFms.IntakeRequest, Jarvis5.Dtos.EaFms.IntakeRequestResponseDto>()
            .ForMember(d => d.IntakeRequestId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.RequiredDate, opt => opt.MapFrom(s => s.RequiredDate));
        CreateMap<Jarvis5.Entities.EaFms.IntakeClassification, Jarvis5.Dtos.EaFms.IntakeClassificationResponseDto>();
        CreateMap<Jarvis5.Entities.EaFms.WorkflowInstance, Jarvis5.Dtos.EaFms.WorkflowResponseDto>()
            .ForMember(d => d.IntakeRequestId, opt => opt.MapFrom(s => s.IntakeRequestId))
            .ForMember(d => d.BusinessModuleId, opt => opt.MapFrom(s => s.BusinessModuleId))
            .ForMember(d => d.BusinessRecordId, opt => opt.MapFrom(s => s.BusinessRecordId));
        CreateMap<Jarvis5.Entities.EaFms.WorkflowHistory, Jarvis5.Dtos.EaFms.WorkflowHistoryResponseDto>();
        CreateMap<Jarvis5.Entities.EaFms.Followup, Jarvis5.Dtos.EaFms.FollowupResponseDto>()
            .ForMember(d => d.Remark, opt => opt.MapFrom(s => s.Note))
            .ForMember(d => d.EaTaskId, opt => opt.Ignore())
            .ForMember(d => d.ModuleName, opt => opt.Ignore())
            .ForMember(d => d.Task, opt => opt.Ignore())
            .ForMember(d => d.Stage, opt => opt.Ignore())
            .ForMember(d => d.IsPaused, opt => opt.Ignore())
            .ForMember(d => d.BusinessModuleCode, opt => opt.Ignore())
            .ForMember(d => d.BusinessModuleName, opt => opt.Ignore())
            .ForMember(d => d.BusinessRecordTitle, opt => opt.Ignore())
            .ForMember(d => d.PriorityLevelName, opt => opt.Ignore())
            .ForMember(d => d.Recipient, opt => opt.Ignore())
            .ForMember(d => d.WhatsApp, opt => opt.Ignore())
            .ForMember(d => d.Escalation, opt => opt.Ignore())
            .ForMember(d => d.IsCompleted, opt => opt.Ignore())
            .ForMember(d => d.IsOverdue, opt => opt.Ignore());
        CreateMap<Jarvis5.Entities.EaFms.Escalation, Jarvis5.Dtos.EaFms.EscalationResponseDto>()
            .ForMember(d => d.IntakeRequestId, opt => opt.MapFrom(s => s.IntakeRequestId))
            .ForMember(d => d.BusinessModuleId, opt => opt.MapFrom(s => s.BusinessModuleId))
            .ForMember(d => d.BusinessRecordId, opt => opt.MapFrom(s => s.BusinessRecordId))
            .ForMember(d => d.EscalationLevelName, opt => opt.Ignore())
            .ForMember(d => d.NextEscalationLevelId, opt => opt.MapFrom(s => s.NextEscalationLevelId))
            .ForMember(d => d.NextEscalationAt, opt => opt.MapFrom(s => s.NextEscalationAt));
        CreateMap<Jarvis5.Entities.EaFms.EscalationLevel, Jarvis5.Dtos.EaFms.EscalationLevelResponseDto>();
        CreateMap<Jarvis5.Entities.EaFms.Meeting, Jarvis5.Dtos.EaFms.MeetingListItemResponseDto>()
            .ForMember(d => d.MeetingId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.Type, opt => opt.MapFrom(s => s.MeetingType))
            .ForMember(d => d.Subtype, opt => opt.MapFrom(s => s.Category))
            .ForMember(d => d.Doers, opt => opt.MapFrom(s => Jarvis5.Common.EaFms.MeetingDoers.ToDtos(s)))
            .ForMember(d => d.MeetingNumber, opt => opt.MapFrom(s => s.MeetingNumber))
            .ForMember(d => d.ModuleId, opt => opt.Ignore())
            .ForMember(d => d.ModuleName, opt => opt.Ignore())
            .ForMember(d => d.TatMinutes, opt => opt.Ignore())
            .ForMember(d => d.Priority, opt => opt.Ignore())
            .ForMember(d => d.DoerId, opt => opt.Ignore())
            .ForMember(d => d.DoerName, opt => opt.Ignore())
            .ForMember(d => d.CreatedDate, opt => opt.MapFrom(s => s.CreatedDate))
            .ForMember(d => d.ModifiedDate, opt => opt.MapFrom(s => s.ModifiedDate));

        CreateMap<Jarvis5.Entities.EaFms.Meeting, Jarvis5.Dtos.EaFms.MeetingDetailResponseDto>()
            .ForMember(d => d.MeetingId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.Type, opt => opt.MapFrom(s => s.MeetingType))
            .ForMember(d => d.Subtype, opt => opt.MapFrom(s => s.Category))
            .ForMember(d => d.Doers, opt => opt.MapFrom(s => Jarvis5.Common.EaFms.MeetingDoers.ToDtos(s)))
            .ForMember(d => d.ModuleId, opt => opt.Ignore())
            .ForMember(d => d.ModuleName, opt => opt.Ignore())
            .ForMember(d => d.TatMinutes, opt => opt.Ignore())
            .ForMember(d => d.Task, opt => opt.Ignore())
            .ForMember(d => d.AllottedTatMinutes, opt => opt.Ignore())
            .ForMember(d => d.EaTaskId, opt => opt.Ignore())
            .ForMember(d => d.Priority, opt => opt.Ignore())
            .ForMember(d => d.CreatedDate, opt => opt.MapFrom(s => s.CreatedDate))
            .ForMember(d => d.CreatedBy, opt => opt.MapFrom(s => s.CreatedBy))
            .ForMember(d => d.ModifiedDate, opt => opt.MapFrom(s => s.ModifiedDate));

        CreateMap<Jarvis5.Entities.EaFms.MeetingAgenda, Jarvis5.Dtos.EaFms.MeetingAgendaDto>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.OrderNo, opt => opt.MapFrom(s => s.SequenceNumber))
            .ForMember(d => d.PreparedAt, opt => opt.MapFrom(s => s.PreparedAt));

        CreateMap<Jarvis5.Entities.EaFms.MeetingAttendee, Jarvis5.Dtos.EaFms.MeetingAttendeeDto>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.Name, opt => opt.MapFrom(s => s.ParticipantName))
            .ForMember(d => d.ConfirmedAt, opt => opt.MapFrom(s => s.ConfirmedAt))
            .ForMember(d => d.AttendedAt, opt => opt.MapFrom(s => s.AttendedAt));

        CreateMap<Jarvis5.Entities.EaFms.MeetingMinutes, Jarvis5.Dtos.EaFms.MeetingMinutesDto>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.Notes, opt => opt.MapFrom(s => s.DiscussionNotes))
            .ForMember(d => d.PreparedAt, opt => opt.MapFrom(s => s.PreparedAt))
            .ForMember(d => d.SubmittedAt, opt => opt.MapFrom(s => s.SubmittedAt))
            .ForMember(d => d.RevisionNo, opt => opt.MapFrom(s => s.RevisionNumber));

        CreateMap<Jarvis5.Entities.EaFms.MeetingDecision, Jarvis5.Dtos.EaFms.MeetingDecisionDto>();
        CreateMap<Jarvis5.Entities.EaFms.MeetingAction, Jarvis5.Dtos.EaFms.MeetingActionDto>();
        CreateMap<Jarvis5.Entities.EaFms.TatRule, Jarvis5.Dtos.EaFms.TatRuleDto>()
            .ForMember(d => d.ModuleId, opt => opt.MapFrom(s => s.BusinessModuleId))
            .ForMember(d => d.ModuleName, opt => opt.MapFrom(s => s.BusinessModule.Name));
        CreateMap<Jarvis5.Entities.EaFms.WorkPause, Jarvis5.Dtos.EaFms.WorkPauseResponseDto>();
        CreateMap<Jarvis5.Entities.EaFms.WorkPause, Jarvis5.Dtos.EaFms.WaitingResponseDto>();
    }
}
