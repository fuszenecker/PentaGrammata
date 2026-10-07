using System;
using System.Threading.Tasks;

namespace PentaGrammata.Presentation;

/// <summary>Schedules UI-bound work on the application's UI thread.</summary>
public interface IUiDispatcher
{
    Task InvokeAsync(Action action);

    Task<T> InvokeAsync<T>(Func<T> action);
}
