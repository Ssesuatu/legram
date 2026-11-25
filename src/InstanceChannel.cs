using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Legram
{
    internal sealed class InstanceChannel : IDisposable
    {
        private static string Prefix
        {
            get { return @"Local\LegramEngine" + (Environment.GetEnvironmentVariable("LEGRAM_SESSION") ?? ""); }
        }

        public static bool ElectionExists()
        {
            Mutex m;
            if (!Mutex.TryOpenExisting(Prefix, out m)) return false;
            m.Dispose();
            return true;
        }

        public readonly EventWaitHandle Open;
        public readonly EventWaitHandle Purge;
        public readonly EventWaitHandle Quit;
        public readonly EventWaitHandle Ready;
        public readonly EventWaitHandle Ack;
        private readonly Mutex owner;
        public readonly bool Primary;

        private string PidFile
        {
            get { return Path.Combine(Log.DirectoryPath, "primary" + (Environment.GetEnvironmentVariable("LEGRAM_SESSION") ?? "") + ".pid"); }
        }

        private static EventWaitHandle Existing(string name)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 5000)
            {
                try { return EventWaitHandle.OpenExisting(name); }
                catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(20); }
            }
            throw new TimeoutException("IPC event " + name);
        }

        public InstanceChannel()
        {
            Mutex existing;
            bool hasOwner = Mutex.TryOpenExisting(Prefix, out existing);
            if (hasOwner)
            {
                owner = existing;
                try { Primary = owner.WaitOne(0); }
                catch (AbandonedMutexException) { Primary = true; }
                if (!Primary)
                {
                    Open = Existing(Prefix + "Open");
                    Purge = Existing(Prefix + "Purge");
                    Quit = Existing(Prefix + "Quit");
                    Ready = Existing(Prefix + "Ready");
                    Ack = Existing(Prefix + "Ack");
                    return;
                }
            }

            Open = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + "Open");
            Purge = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + "Purge");
            Quit = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + "Quit");
            Ready = new EventWaitHandle(false, EventResetMode.ManualReset, Prefix + "Ready");
            Ack = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + "Ack");

            if (!hasOwner)
            {
                owner = new Mutex(false, Prefix);
                try { Primary = owner.WaitOne(0); }
                catch (AbandonedMutexException) { Primary = true; }
            }
            if (Primary)
            {
                Ready.Reset();
                Open.Reset();
                Purge.Reset();
                Quit.Reset();
                Ack.Reset();
            }
        }

        public void Listening()
        {
            Directory.CreateDirectory(Log.DirectoryPath);
            File.WriteAllText(PidFile, Process.GetCurrentProcess().Id.ToString());
            Ready.Set();
        }

        public int Send(bool quit, bool purge, bool open)
        {
            if (!quit && !purge && !open) return 0;
            try
            {
                using (Mutex sender = new Mutex(false, Prefix + "Sender"))
                {
                    bool acquired = false;
                    try
                    {
                        try { acquired = sender.WaitOne(5000); }
                        catch (AbandonedMutexException) { acquired = true; }
                        if (!acquired) throw new TimeoutException("IPC sender");
                        if (!Ready.WaitOne(10000)) throw new TimeoutException("IPC ready");

                        Process primary = null;
                        if (quit && File.Exists(PidFile))
                        {
                            int pid;
                            if (int.TryParse(File.ReadAllText(PidFile).Trim(), out pid))
                            {
                                try { primary = Process.GetProcessById(pid); } catch { primary = null; }
                            }
                        }

                        using (primary)
                        {
                            Ack.Reset();
                            if (quit) Quit.Set();
                            else if (purge) Purge.Set();
                            else Open.Set();

                            if (!Ack.WaitOne(5000)) throw new TimeoutException("IPC acknowledgement");
                            if (primary != null && !primary.WaitForExit(10000)) throw new TimeoutException("Primary exit");
                        }
                        return 0;
                    }
                    finally
                    {
                        if (acquired) sender.ReleaseMutex();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("IPC Send", e);
                return 2;
            }
        }

        public void Dispose()
        {
            try
            {
                if (Primary && owner != null)
                {
                    Ready.Reset();
                    owner.ReleaseMutex();
                }
            }
            catch { }
            if (owner != null) owner.Dispose();
            if (Open != null) Open.Dispose();
            if (Purge != null) Purge.Dispose();
            if (Quit != null) Quit.Dispose();
            if (Ready != null) Ready.Dispose();
            if (Ack != null) Ack.Dispose();
        }
    }
}
