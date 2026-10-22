namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private static void CheckInstanceMutex()
    {
        var name = @"Local\MemReduct.Fluent.Check." + Guid.NewGuid().ToString("N");
        using (var owner = App.AcquireInstanceMutex(name, TimeSpan.Zero))
        {
            Require(owner is not null, "An unused single-instance mutex can be acquired");
            var rejected = false;
            Exception? failure = null;
            var contender = new Thread(() =>
            {
                try
                {
                    using var competing = App.AcquireInstanceMutex(name, TimeSpan.Zero);
                    rejected = competing is null;
                    competing?.ReleaseMutex();
                }
                catch (Exception error) { failure = error; }
            });
            contender.IsBackground = true;
            try
            {
                contender.Start();
                Require(contender.Join(TimeSpan.FromSeconds(3)) && failure is null && rejected,
                    "A competing thread receives no mutex and cannot release the current instance's ownership");
            }
            finally { owner!.ReleaseMutex(); }
        }
        using (var reopened = App.AcquireInstanceMutex(name, TimeSpan.Zero))
        {
            Require(reopened is not null, "The mutex is available after the original instance releases it");
            reopened!.ReleaseMutex();
        }

        using var abandoned = new Mutex(false, name + ".Abandoned");
        var previousOwner = new Thread(() => abandoned.WaitOne()) { IsBackground = true };
        previousOwner.Start();
        Require(previousOwner.Join(TimeSpan.FromSeconds(3)), "The synthetic previous owner has exited");
        using (var recovered = App.AcquireInstanceMutex(name + ".Abandoned", TimeSpan.Zero))
        {
            Require(recovered is not null, "An abandoned mutex transfers ownership to the new instance");
            recovered!.ReleaseMutex();
        }

        var invalid = false;
        try { using var unused = App.AcquireInstanceMutex(name, TimeSpan.FromMilliseconds(-2)); }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        Require(invalid, "A wait failure propagates without returning an unowned mutex");
    }
}
