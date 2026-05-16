namespace DiscordPreset.Modules.FilesBot.Converts;

public static class ScToPngFix
{
  private static readonly string ConverterPath =
      @"/home/metchi/MtcCave/CSHARP_LEARNING/DiscordPreset/Modules/FilesBot/Converts/SCTex";

  private static readonly HttpClient Http = new();

  public static async Task HandleConverter(SocketMessage message)
  {
    IAttachment? attachment = null;

    var reference = new MessageReference(message.Id);

    if (message.Attachments.Count > 0)
    {
      attachment = message.Attachments
          .FirstOrDefault(a =>
              a.Filename.EndsWith(".sc", StringComparison.OrdinalIgnoreCase));
    }

    if (attachment == null && message.Reference?.MessageId != null)
    {
      var referencedMsg = await message.Channel
          .GetMessageAsync(message.Reference.MessageId.Value);

      if (referencedMsg is IUserMessage refUserMsg)
      {
        attachment = refUserMsg.Attachments
            .FirstOrDefault(a =>
                a.Filename.EndsWith(".sc", StringComparison.OrdinalIgnoreCase));
      }
    }

    if (attachment == null)
    {
      await message.Channel.SendMessageAsync(
          "Attach or reply to a .sc file.",
          messageReference: reference
      );
      return;
    }

    string tempDir = Path.Combine(
        Path.GetTempPath(),
        "sc_" + Guid.NewGuid().ToString("N")
    );

    Directory.CreateDirectory(tempDir);

    string scFile = Path.Combine(tempDir, attachment.Filename);

    try
    {
      await using (var stream = await Http.GetStreamAsync(attachment.Url))
      await using (var fs = File.Create(scFile))
      {
        await stream.CopyToAsync(fs);
      }

      await RunShellCommand($"chmod +x \"{ConverterPath}\"");

      var psi = new ProcessStartInfo
      {
        FileName = ConverterPath,
        Arguments = $"decode \"{scFile}\"",

        WorkingDirectory = Path.GetDirectoryName(ConverterPath)!,

        RedirectStandardOutput = true,
        RedirectStandardError = true,

        UseShellExecute = false,
        CreateNoWindow = true
      };

      using Process proc = Process.Start(psi)!;

      string stdout = await proc.StandardOutput.ReadToEndAsync();
      string stderr = await proc.StandardError.ReadToEndAsync();

      await proc.WaitForExitAsync();

      Console.WriteLine(stdout);
      Console.WriteLine(stderr);

      var pngFiles = Directory.GetFiles(
          tempDir,
          "*.png",
          SearchOption.AllDirectories
      );

      if (pngFiles.Length == 0)
      {
        await message.Channel.SendMessageAsync(
            $"No PNGs were generated.\n\n" +
            $"Exit: {proc.ExitCode}\n" +
            $"STDERR:\n```{Trim(stderr)}```",
            messageReference: reference
        );

        return;
      }

      foreach (var png in pngFiles)
      {
        await using var fs = File.OpenRead(png);

        await message.Channel.SendFileAsync(
            stream: fs,
            filename: Path.GetFileName(png),
            messageReference: reference
        );
      }
    }
    catch (Exception ex)
    {
      await message.Channel.SendMessageAsync(
          $"Error:\n```{ex}```",
          messageReference: reference
      );
    }
    finally
    {
      try
      {
        if (Directory.Exists(tempDir))
          Directory.Delete(tempDir, true);
      }
      catch { }
    }
  }

  private static async Task RunShellCommand(string command)
  {
    var psi = new ProcessStartInfo
    {
      FileName = "/bin/bash",
      Arguments = $"-c \"{command}\"",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var proc = Process.Start(psi)!;
    await proc.WaitForExitAsync();
  }

  private static string Trim(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return "(empty)";

    return text.Length > 1800 ? text[..1800] : text;
  }
}
