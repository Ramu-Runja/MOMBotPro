using System.Collections.Concurrent;

namespace MOMBotPro.API.Services;

public class PipelineCancellationService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokens = new();

    public CancellationToken Register(string pipelineId)
    {
        var cts = new CancellationTokenSource();
        _tokens[pipelineId] = cts;
        return cts.Token;
    }

    public bool Cancel(string pipelineId)
    {
        if (_tokens.TryRemove(pipelineId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }
        return false;
    }

    public void Remove(string pipelineId)
    {
        if (_tokens.TryRemove(pipelineId, out var cts))
            cts.Dispose();
    }
}
