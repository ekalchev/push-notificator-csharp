using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

static class Extensions
{
    public static async void Forget(this Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {

        }
    }
}
