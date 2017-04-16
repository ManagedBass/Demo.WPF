using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using ManagedBass;
using ManagedBass.Fx;
using Microsoft.Win32;

namespace Revic
{
    public class PlaylistLabel : ContentControl
    {
        static readonly Lazy<SaveFileDialog> ReverseSaveDialog = new Lazy<SaveFileDialog>(() => new SaveFileDialog
        {
            Filter = "Mp3 Audio|*.mp3|Windows Media Audio|*.wma|Wave Audio|*.wav"
        });

        public PlaylistLabel(string FileName)
        {
            ContextMenu = new ContextMenu();

            var saveReverseMenuItem = new MenuItem { Header = "Save Reverse" };

            saveReverseMenuItem.Click += (s, e) => SaveReverse(FileName);

            ContextMenu.Items.Add(saveReverseMenuItem);

            Content = FileName;

            PreviewMouseLeftButtonDown += (s, e) => DragDrop.DoDragDrop(this, FileName, DragDropEffects.Copy);
        }

        static void SaveReverse(string FilePath)
        {
            try
            {
                var sfd = ReverseSaveDialog.Value;
                sfd.FileName = Path.GetFileNameWithoutExtension(FilePath) + ".Reverse";

                if (!sfd.ShowDialog().Value)
                    return;

                var fc = Bass.CreateStream(FilePath, Flags: BassFlags.Decode);
                var rc = BassFx.ReverseCreate(fc, 2, BassFlags.Decode | BassFlags.FxFreeSource);

                var wf = WaveFormat.FromChannel(fc);
                
                var writer = new WaveFileWriter(new FileStream(sfd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read), wf);
                
                var blockLength = (int) Bass.ChannelSeconds2Bytes(rc, 2);

                var buffer = new byte[blockLength];

                var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                while (Bass.ChannelIsActive(rc) == PlaybackState.Playing)
                {
                    var bytesReceived = Bass.ChannelGetData(rc, gch.AddrOfPinnedObject(), blockLength);
                    writer.Write(buffer, bytesReceived);
                }

                gch.Free();

                writer.Dispose();
                
                Bass.StreamFree(rc);

                MessageBox.Show("Saved");
            }
            catch (Exception e) { MessageBox.Show($"Failed\n\n{e}"); }
        }
    }
}