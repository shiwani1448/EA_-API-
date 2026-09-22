from pathlib import Path
import hashlib,json,re
root=Path.cwd()
files=[p for base in ['Common','Controllers','DTOs','Data','Entities','Migrations','Repositories','Services','Tests'] for p in (root/base).rglob('*.cs') if not any(x in p.parts for x in ['bin','obj','artifacts'])]+[root/'Program.cs']
Path('artifacts/task-review-scope/baseline.json').write_text(json.dumps({str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},indent=2))
def edit(name,fn):
 p=Path(name); old=p.read_text(encoding='utf-8-sig'); new=fn(old)
 if new!=old:p.write_text(new,encoding='utf-8',newline='')
for name in ['Controllers/MeetingsLifecycleController.cs','Controllers/EaFms/TravelRequestsController.cs']:
 def cut(s):
  pos=s.index('    // TASK REVIEW / REWORK')
  start=s.rfind('    // =====',0,pos)
  return s[:start].rstrip()+'\n}\n'
 edit(name,cut)
edit('Services/EaFms/IMeetingLifecycleService.cs',lambda s:s[:s.index('    /// <summary>Task Review/Rework')].rstrip()+'\n}\n')
edit('Services/EaFms/ITravelRequestService.cs',lambda s:s[:s.rfind('    /// <summary>',0,s.index('    /// Task Review/Rework'))].rstrip()+'\n}\n')
def cut_wrappers(s):
 pos=s.index('    // TASK REVIEW / REWORK'); start=s.rfind('    // =====',0,pos)
 end=s.index('    /// <summary>\n    /// Meeting -> Doer Delegation',pos)
 return s[:start]+s[end:]
edit('Services/EaFms/MeetingLifecycleService.cs',cut_wrappers)
for name in ['Services/EaFms/MeetingService.cs','Services/EaFms/MeetingLifecycleService.cs','Services/EaFms/TravelRequestService.cs']:
 def deps(s):
  s=re.sub(r'^    private readonly ITaskReviewService _taskReview;\n','',s,flags=re.M)
  s=s.replace(', ITaskReviewService taskReview)',')')
  s=re.sub(r',\n        ITaskReviewService taskReview\)',')',s)
  s=re.sub(r'^        _taskReview = taskReview;\n','',s,flags=re.M)
  if 'Meeting' in name:
   s=re.sub(r'^.*(?:response\.ReviewSummary =|item\.ReviewSummary =|dto\.ReviewSummary =|var reviewSummaries =).+\n','',s,flags=re.M)
  return s
 edit(name,deps)
for name in ['DTOs/EaFms/MeetingDetailResponseDto.cs','DTOs/EaFms/MeetingListItemResponseDto.cs','DTOs/EaFms/MeetingLifecycleDtos.cs','DTOs/EaFms/TravelRequestDtos.cs','DTOs/EaFms/TravelActionDtos.cs']:
 def props(s):
  s=re.sub(r'^\s*/// <summary>Central Task Review/Rework[^\n]*\n','\n',s,flags=re.M)
  return re.sub(r'^    public TaskReviewSummaryDto ReviewSummary[^\n]*\n','',s,flags=re.M)
 edit(name,props)
def travel(s):
 s=re.sub(r'^.*var reviewSummary = await _taskReview.GetCurrentAsync[^\n]*\n','',s,flags=re.M)
 s=re.sub(r'^.*var reviewSummaries = await _taskReview.BatchGetCurrentAsync[^\n]*\n','',s,flags=re.M)
 s=s.replace('ToDetailDto(entity, cycle, reviewSummary)','ToDetailDto(entity, cycle)')
 s=s.replace(', await _taskReview.GetCurrentAsync(entity.EaTaskId, ct)','')
 s=s.replace(', reviewSummaries.GetValueOrDefault(e.EaTaskId)','')
 s=s.replace(', TaskReviewSummaryDto? reviewSummary = null','')
 return re.sub(r'^\s*ReviewSummary = reviewSummary \?\? new TaskReviewSummaryDto\(\),?\n','\n',s,flags=re.M)
edit('Services/EaFms/TravelRequestService.cs',travel)
edit('Services/EaFms/TravelRequestService.Lifecycle.cs',lambda s:travel(s.replace(', await _taskReview.GetCurrentAsync(parent.EaTaskId, ct)','')))
for name in ['Services/EaFms/TravelRequestService.Review.cs','Tests/EaFms/TaskReview/MeetingTaskReviewTests.cs','Tests/EaFms/TaskReview/TravelTaskReviewTests.cs']:
 p=(root/name).resolve(); assert p.is_relative_to(root); p.unlink()
# Remove only the final constructor argument for affected services in tests, including target-typed new.
for p in Path('Tests').rglob('*.cs'):
 if any(x in p.parts for x in ['bin','obj','artifacts']):continue
 def args(s):
  pattern=r'new\s+(?:(MeetingService|MeetingLifecycleService|TravelRequestService)\s*)?\('
  changes=[]
  for m in re.finditer(pattern,s):
   if not m.group(1):
    # Target-typed Travel factory declarations use => new(...).
    if 'Travel' not in str(p) or not s[max(0,m.start()-120):m.start()].rstrip().endswith('=>'):continue
   start=m.end(); depth=1; commas=[]; i=start; quoted=False; escape=False
   while i<len(s) and depth:
    c=s[i]
    if quoted:
     if escape:escape=False
     elif c=='\\':escape=True
     elif c=='"':quoted=False
    elif c=='"':quoted=True
    elif c=='(':depth+=1
    elif c==')':depth-=1
    elif c==',' and depth==1:commas.append(i)
    i+=1
   if not commas:continue
   last=s[commas[-1]+1:i-1].strip()
   if 'TaskReview' in last or last=='taskReview':changes.append((commas[-1],i-1))
  for a,b in reversed(changes):s=s[:a]+s[b:]
  return s
 edit(str(p),args)
