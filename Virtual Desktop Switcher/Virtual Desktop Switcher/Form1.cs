using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Virtual_Desktop_Switcher
{   //With a LOT of help from http://stackoverflow.com/questions/32416843/altering-win10-virtual-desktop-behavior/32417530#32417530
    //Icon made by http://icon-works.com from Flaticon (www.flaticon.com). Licensed under Creative Commons CC BY 3.0 (http://creativecommons.org/licenses/by/3.0/)
    public partial class MainForm : Form
    {
        //Some imports for the shortcut key
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        const int HOTKEY_ID = 1;

        int createdDesktop;
        int numDesktopsAtStart = 0;
        public MainForm()
        {
            InitializeComponent();
            //Register the hotkey
            //The modifier is a sum of the modifier values:
            //  Alt = 1
            //  Ctrl = 2
            //  Shift = 4
            //  Wind = 8
            RegisterHotKey(this.Handle, HOTKEY_ID, 3, (int) Keys.D1); //Therefore Alt+Ctrl+1
        }

        //Get typed keys and check for our hotkey
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HOTKEY_ID)
            {   //This means that the hotkey has been pressed so do something
                switchDesktops();
            }
            base.WndProc(ref m);
        }

        private void leftButton_Click(object sender, EventArgs e)
        {
            moveLeft();
        }

        private void rightButton_Click(object sender, EventArgs e)
        {
            moveRight();
        }

        private void createButton_Click(object sender, EventArgs e)
        {
            createDesktop();
        }

        private void moveLeft()
        {
            var curr = Desktop.FromWindow(this.Handle);
            Debug.Assert(curr.Equals(Desktop.Current));

            var left = curr.Left;
            if (left == null) left = Desktop.FromIndex(Desktop.Count - 1);
            if (left != null)
            {
                left.MoveWindow(this.Handle);
                left.MakeVisible();
                this.BringToFront();
                Debug.Assert(left.IsVisible);
            }
            indexLabel.Text = curIndex().ToString();
        }

        private void moveRight()
        {
            var curr = Desktop.FromWindow(this.Handle);
            Debug.Assert(curr.Equals(Desktop.Current));
            var right = curr.Right;
            if (right == null) right = Desktop.FromIndex(0);
            if (right != null)
            {
                right.MoveWindow(this.Handle);
                right.MakeVisible();
                this.BringToFront();
                Debug.Assert(right.IsVisible);
            }
            indexLabel.Text = curIndex().ToString();
        }

        private void createDesktop()
        {
            var desk = Desktop.Create();
            desk.MoveWindow(this.Handle);
            desk.MakeVisible();
            Debug.Assert(desk.IsVisible);
            Debug.Assert(desk.Equals(Desktop.Current));
            indexLabel.Text = curIndex().ToString();
        }

        private void deleteCurrentDesktop()
        {
            var curr = Desktop.FromWindow(this.Handle);
            var next = curr.Left;
            if (next == null) next = curr.Right;
            if (next != null && next != curr)
            {
                next.MoveWindow(this.Handle);
                curr.Remove(next);
                Debug.Assert(next.IsVisible);
            }
            indexLabel.Text = curIndex().ToString();
        }
        
        private void deleteButton_Click(object sender, EventArgs e)
        {
            deleteCurrentDesktop();
        }

        public int curIndex()
        {
            for (int i = 0; i < Desktop.Count; i++)
            {
                if (DesktopManager.GetDesktop(i) == DesktopManager.Manager.GetCurrentDesktop())
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private void bossButton_Click(object sender, EventArgs e)
        {
            switchDesktops();
        }

        private void switchDesktops()
        {
            //The form needs to be active on the desktop for this to work
            FormWindowState oldState = WindowState;
            if (oldState!=FormWindowState.Normal)
            {
                Show();
                WindowState = FormWindowState.Normal;
            }

            if (Desktop.Count < 2)
            {   //If there's only one desktop (or 0, but that can't happen, I hope), then create a new one
                createDesktop();
                createdDesktop = curIndex();    //Log that we've created a desktop
                numDesktopsAtStart = Desktop.Count;
            }
            else
            {   //If we've already got a second desktop, then just switch back to the original one
                if (curIndex() == 1)
                {
                    moveRight();
                }
                else
                {
                    moveLeft();
                }
            }

            //Reset the form if it wasn't maximized before
            WindowState = oldState;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (createdDesktop != 0 && Desktop.Count >= numDesktopsAtStart)
            {
                DialogResult result = MessageBox.Show("You have created a new desktop. Would you like to close it now?", "Close desktop?", MessageBoxButtons.YesNo);
                if(result == DialogResult.Yes)
                {   //Then close the desktop we created
                    if (curIndex()==createdDesktop)
                    {
                        deleteCurrentDesktop();
                    }
                    else
                    {   //So we're in another desktop, so move left or right until we hit it
                        do
                        {   //We could just cycle, but it might be faster to move in the right direction
                            if (curIndex()>createdDesktop)
                            {
                                moveLeft();
                            }
                            else
                            {
                                moveRight();
                            }
                        } while (curIndex() != createdDesktop);
                        //So we've reached the created desktop
                        //Close it
                        deleteCurrentDesktop();
                        //And now continue with app shutdown
                    }
                }
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState==FormWindowState.Minimized)
            {
                notifyIcon.Visible = true;
                Hide(); //Hide the application from the taskbar
            }
            else
            {
                notifyIcon.Visible = false;
            }
        }

        private void notifyIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;   //Un-minimize the app window
        }
    }

    internal static class Guids
    {
        public static readonly Guid CLSID_ImmersiveShell =
            new Guid(0xC2F03A33, 0x21F5, 0x47FA, 0xB4, 0xBB, 0x15, 0x63, 0x62, 0xA2, 0xF2, 0x39);
        public static readonly Guid CLSID_VirtualDesktopManagerInternal =
            new Guid(0xC5E0CDCA, 0x7B6E, 0x41B2, 0x9F, 0xC4, 0xD9, 0x39, 0x75, 0xCC, 0x46, 0x7B);
        public static readonly Guid CLSID_VirtualDesktopManager =
            new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
        public static readonly Guid IID_IVirtualDesktopManagerInternal =
            new Guid("AF8DA486-95BB-4460-B3B7-6E7A6B2962B5");
        public static readonly Guid IID_IVirtualDesktop =
            new Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4");
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4")]
    internal interface IVirtualDesktop
    {
        void notimpl1(); // void IsViewVisible(IApplicationView view, out int visible);
        Guid GetId();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("AF8DA486-95BB-4460-B3B7-6E7A6B2962B5")]
    internal interface IVirtualDesktopManagerInternal
    {
        int GetCount();
        void notimpl1();  // void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
        void notimpl2();  // void CanViewMoveDesktops(IApplicationView view, out int itcan);
        IVirtualDesktop GetCurrentDesktop();
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
        void SwitchDesktop(IVirtualDesktop desktop);
        IVirtualDesktop CreateDesktop();
        void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
        IVirtualDesktop FindDesktop(ref Guid desktopid);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    internal interface IVirtualDesktopManager
    {
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
        Guid GetWindowDesktopId(IntPtr topLevelWindow);
        void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
    internal interface IObjectArray
    {
        void GetCount(out int count);
        void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)]out object obj);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    internal interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    public class Desktop
    {
        public static int Count
        {
            // Returns the number of desktops
            get { return DesktopManager.Manager.GetCount(); }
        }

        public static Desktop Current
        {
            // Returns current desktop
            get { return new Desktop(DesktopManager.Manager.GetCurrentDesktop()); }
        }

        public static Desktop FromIndex(int index)
        {
            // Create desktop object from index 0..Count-1
            return new Desktop(DesktopManager.GetDesktop(index));
        }

        public static Desktop FromWindow(IntPtr hWnd)
        {
            // Creates desktop object on which window <hWnd> is displayed
            Guid id = DesktopManager.WManager.GetWindowDesktopId(hWnd);
            return new Desktop(DesktopManager.Manager.FindDesktop(ref id));
        }

        public static Desktop Create()
        {
            // Create a new desktop
            return new Desktop(DesktopManager.Manager.CreateDesktop());
        }

        public void Remove(Desktop fallback = null)
        {
            // Destroy desktop and switch to <fallback>
            var back = fallback == null ? DesktopManager.GetDesktop(0) : fallback.itf;
            DesktopManager.Manager.RemoveDesktop(itf, back);
        }

        public bool IsVisible
        {
            // Returns <true> if this desktop is the current displayed one
            get { return object.ReferenceEquals(itf, DesktopManager.Manager.GetCurrentDesktop()); }
        }

        public void MakeVisible()
        {
            // Make this desktop visible
            DesktopManager.Manager.SwitchDesktop(itf);
        }

        public Desktop Left
        {
            // Returns desktop at the left of this one, null if none
            get
            {
                IVirtualDesktop desktop;
                int hr = DesktopManager.Manager.GetAdjacentDesktop(itf, 3, out desktop);
                if (hr == 0) return new Desktop(desktop);
                else return null;
            }
        }

        public Desktop Right
        {
            // Returns desktop at the right of this one, null if none
            get
            {
                IVirtualDesktop desktop;
                int hr = DesktopManager.Manager.GetAdjacentDesktop(itf, 4, out desktop);
                if (hr == 0) return new Desktop(desktop);
                else return null;
            }
        }

        public void MoveWindow(IntPtr handle)
        {
            // Move window <handle> to this desktop
            DesktopManager.WManager.MoveWindowToDesktop(handle, itf.GetId());
        }

        public bool HasWindow(IntPtr handle)
        {
            // Returns true if window <handle> is on this desktop
            return itf.GetId() == DesktopManager.WManager.GetWindowDesktopId(handle);
        }

        public override int GetHashCode()
        {
            return itf.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            var desk = obj as Desktop;
            return desk != null && object.ReferenceEquals(this.itf, desk.itf);
        }

        private IVirtualDesktop itf;
        private Desktop(IVirtualDesktop itf) { this.itf = itf; }
    }

    internal static class DesktopManager
    {
        static DesktopManager()
        {
            var shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell));
            Manager = (IVirtualDesktopManagerInternal)shell.QueryService(Guids.CLSID_VirtualDesktopManagerInternal, Guids.IID_IVirtualDesktopManagerInternal);
            WManager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_VirtualDesktopManager));
        }

        internal static IVirtualDesktop GetDesktop(int index)
        {
            int count = Manager.GetCount();
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException("index");
            IObjectArray desktops;
            Manager.GetDesktops(out desktops);
            object objdesk;
            desktops.GetAt(index, Guids.IID_IVirtualDesktop, out objdesk);
            Marshal.ReleaseComObject(desktops);
            return (IVirtualDesktop)objdesk;
        }

        internal static IVirtualDesktopManagerInternal Manager;
        internal static IVirtualDesktopManager WManager;
    }
}
