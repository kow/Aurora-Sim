﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Aurora.Framework
{
    public class ThreadMonitor
    {
        protected internal class InternalHeartbeat
        {
            public Heartbeat heartBeat;
            public int millisecondTimeOut;
        }
        public delegate bool Heartbeat();
        protected internal delegate void FireEvent(Heartbeat thread);
        protected Object m_lock = new Object();
        protected List<InternalHeartbeat> m_heartbeats = new List<InternalHeartbeat>();
        protected int m_timesToIterate = 0;
        private int m_sleepTime = 0;

        /// <summary>
        /// Add this delegate to the tracker so that it can run.
        /// </summary>
        /// <param name="millisecondTimeOut">The time that the thread can run before it is forcefully stopped.</param>
        /// <param name="hb">The delegate to run.</param>
        public void StartTrackingThread(int millisecondTimeOut, Heartbeat hb)
        {
            lock (m_lock)
            {
                m_heartbeats.Add(new InternalHeartbeat() { heartBeat = hb, millisecondTimeOut = millisecondTimeOut });
            }
        }

        /// <summary>
        /// Start the thread and run through the threads that are given.
        /// </summary>
        /// <param name="timesToIterate">The number of times to run the delegate.
        /// <remarks>If you set this parameter to 0, it will loop infinitely.</remarks></param>
        /// <param name="sleepTime">The sleep time between each iteration.
        /// <remarks>If you set this parameter to 0, it will loop without sleeping at all.
        /// The sleeping will have to be deal with in the delegates.</remarks></param>
        public void StartMonitor(int timesToIterate, int sleepTime)
        {
            m_timesToIterate = timesToIterate;
            m_sleepTime = sleepTime;

            Thread thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "ThreadMonitor";
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();
        }

        /// <summary>
        /// Run the loop through the heartbeats.
        /// </summary>
        protected internal void Run()
        {
            try
            {
                List<InternalHeartbeat> hbToRemove = null;
                while (m_timesToIterate >= 0)
                {
                    lock (m_lock)
                    {
                        foreach (InternalHeartbeat intHB in m_heartbeats)
                        {
                            bool isRunning = false;
                            if (!CallAndWait (intHB.millisecondTimeOut, intHB.heartBeat, out isRunning))
                            {
                                Console.WriteLine ("WARNING: Could not run Heartbeat in specified limits!");
                            }
                            else if(!isRunning)
                            {
                                if(hbToRemove == null)
                                    hbToRemove = new List<InternalHeartbeat> ();
                                hbToRemove.Add (intHB);
                            }
                        }

                        if (hbToRemove != null)
                        {
                            foreach (InternalHeartbeat intHB in hbToRemove)
                            {
                                m_heartbeats.Remove (intHB);
                            }
                            //Renull it for later
                            hbToRemove = null;
                            if (m_heartbeats.Count == 0) //None left, break
                                break;
                        }
                    }
                    //0 is infinite
                    if (m_timesToIterate != 0)
                    {
                        //Subtract, then see if it is 0, and if it is, it is time to stop
                        m_timesToIterate--;
                        if (m_timesToIterate == 0)
                            break;
                    }
                    if (m_timesToIterate == -1) //Kill signal
                        break;
                    if (m_sleepTime != 0)
                        Thread.Sleep (m_sleepTime);
                }
            }
            catch
            {
            }
            Thread.CurrentThread.Abort();
        }

        /// <summary>
        /// Call the method and wait for it to complete or the max time.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="enumerator"></param>
        /// <returns></returns>
        protected bool CallAndWait(int timeout, Heartbeat enumerator, out bool isRunning)
        {
            isRunning = false;
            bool RetVal = false;
            if (timeout == 0)
            {
                isRunning = enumerator ();
                RetVal = true;
            }
            else
            {
                //The action to fire
                FireEvent wrappedAction = delegate(Heartbeat en)
                {
                    // Set this culture for the thread 
                    // to en-US to avoid number parsing issues
                    OpenSim.Framework.Culture.SetCurrentCulture();
                    en();
                    RetVal = true;
                };

                //Async the action (yeah, this is bad, but otherwise we can't abort afaik)
                IAsyncResult result = wrappedAction.BeginInvoke(enumerator, null, null);
                if (((timeout != 0) && !result.IsCompleted) &&
                    (!result.AsyncWaitHandle.WaitOne(timeout, false) || !result.IsCompleted))
                {
                    isRunning = false;
                    return false;
                }
                else
                {
                    wrappedAction.EndInvoke(result);
                    isRunning = true;
                }
            }
            //Return what we got
            return RetVal;
        }

        public void Stop()
        {
            lock (m_lock)
            {
                //Remove all
                m_heartbeats.Clear();
                //Kill it
                m_timesToIterate = -1;
            }
        }
    }
}
