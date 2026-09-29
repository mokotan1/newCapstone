using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.Sequence
{
    public interface ISequenceHost
    {
        Task WaitAsync(int milliseconds, CancellationToken cancellationToken);

        Task SayAsync(string speaker, string line, CancellationToken cancellationToken);
    }
}
