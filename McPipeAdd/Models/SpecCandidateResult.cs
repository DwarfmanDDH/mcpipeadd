using System.Collections.Generic;

namespace McPipeAdd
{
    public class SpecCandidateResult
    {
        public string SpecName = string.Empty;
        public string PspcPath = string.Empty;
        public string PspxPath = string.Empty;
        public List<PipeCandidate> Candidates = new List<PipeCandidate>();
        public List<string> Errors = new List<string>();
    }
}