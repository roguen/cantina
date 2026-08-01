// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.TestHarness;

public static class HarnessProgram
{
    public static Task<int> Main(string[] args) =>
        HarnessRunner.RunCliAsync(args, Console.Out, Console.Error);
}
