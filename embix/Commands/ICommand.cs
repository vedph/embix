using System.Threading.Tasks;

namespace Embix.Commands
{
    public interface ICommand
    {
        Task Run();
    }
}
