/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

/* Revised Aug, Sept 2009 by Kitto Flora. ODEDynamics.cs replaces
 * ODEVehicleSettings.cs. It and ODEPrim.cs are re-organised:
 * ODEPrim.cs contains methods dealing with Prim editing, Prim
 * characteristics and Kinetic motion.
 * ODEDynamics.cs contains methods dealing with Prim Physical motion
 * (dynamics) and the associated settings. Old Linear and angular
 * motors for dynamic motion have been replace with  MoveLinear()
 * and MoveAngular(); 'Physical' is used only to switch ODE dynamic
 * simualtion on/off; VEHICAL_TYPE_NONE/VEHICAL_TYPE_<other> is to
 * switch between 'VEHICLE' parameter use and general dynamics
 * settings use.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;
using OpenMetaverse;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace Aurora.Physics.AuroraOpenDynamicsEngine
{
    public class AuroraODEDynamics
    {
        public Vehicle Type
        {
            get { return m_type; }
        }

        public IntPtr Body
        {
            get { return m_body; }
        }

        private int frcount = 0;                                        // Used to limit dynamics debug output to
        // every 100th frame

        // private OdeScene m_parentScene = null;
        private IntPtr m_body = IntPtr.Zero;
        private Material m_previousMaterial = Material.Wood;
        //        private IntPtr m_jointGroup = IntPtr.Zero;
        //        private IntPtr m_aMotor = IntPtr.Zero;


        // Vehicle properties
        private Vehicle m_type = Vehicle.TYPE_NONE;                     // If a 'VEHICLE', and what kind
        private Quaternion m_referenceFrame = Quaternion.Identity;   // Axis modifier
        private VehicleFlag m_flags = (VehicleFlag)0;                  // Boolean settings:
        // HOVER_TERRAIN_ONLY
        // HOVER_GLOBAL_HEIGHT
        // NO_DEFLECTION_UP
        // HOVER_WATER_ONLY
        // HOVER_UP_ONLY
        // LIMIT_MOTOR_UP
        // LIMIT_ROLL_ONLY
        private VehicleFlag m_Hoverflags = (VehicleFlag)0;
        private Vector3 m_BlockingEndPoint = Vector3.Zero;
        private Quaternion m_RollreferenceFrame = Quaternion.Identity;
        // Linear properties
        private Vector3 m_linearMotorDirection = Vector3.Zero;          // velocity requested by LSL, decayed by time
        private Vector3 m_linearMotorDirectionLASTSET = Vector3.Zero;   // velocity requested by LSL
        private Vector3 m_dir = Vector3.Zero;                           // velocity applied to body
        private Vector3 m_linearFrictionTimescale = Vector3.Zero;
        private float m_linearMotorDecayTimescale = 0;
        private float m_linearMotorTimescale = 0;
        private Vector3 m_lastLinearVelocityVector = Vector3.Zero;
        private d.Vector3 m_lastPositionVector = new d.Vector3();
        //private bool m_LinearMotorSetLastFrame = false;
        private Vector3 m_linearMotorOffset = Vector3.Zero;

        //Angular properties
        private Vector3 m_angularMotorDirection = Vector3.Zero;         // angular velocity requested by LSL motor
        private int m_angularMotorApply = 0;                            // application frame counter
        private Vector3 m_angularMotorVelocity = Vector3.Zero;          // current angular motor velocity
        private float m_angularMotorTimescale = 0;                      // motor angular velocity ramp up rate
        private float m_angularMotorDecayTimescale = 0;                 // motor angular velocity decay rate
        private Vector3 m_angularFrictionTimescale = Vector3.Zero;      // body angular velocity  decay rate
        private Vector3 m_lastAngularVelocity = Vector3.Zero;           // what was last applied to body
        //       private Vector3 m_lastVertAttractor = Vector3.Zero;             // what VA was last applied to body

        //Deflection properties
        private float m_angularDeflectionEfficiency = 0;
        private float m_angularDeflectionTimescale = 0;
        private float m_linearDeflectionEfficiency = 0;
        private float m_linearDeflectionTimescale = 0;

        //Banking properties
        private float m_bankingEfficiency = 0;
        private float m_bankingMix = 0;
        private float m_bankingTimescale = 0;

        //Hover and Buoyancy properties
        private float m_VhoverHeight = 0f;
        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 0f;
        private float m_VhoverTargetHeight = -1.0f;     // if <0 then no hover, else its the current target height
        private float m_VehicleBuoyancy = 0f;           //KF: m_VehicleBuoyancy is set by VEHICLE_BUOYANCY for a vehicle.
        // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity)
        // KF: So far I have found no good method to combine a script-requested .Z velocity and gravity.
        // Therefore only m_VehicleBuoyancy=1 (0g) will use the script-requested .Z velocity.

        //Attractor properties
        private float m_verticalAttractionEfficiency = 1.0f;        // damped
        private float m_verticalAttractionTimescale = 500f;         // Timescale > 300  means no vert attractor.
        public double Mass;
        private bool m_enabled = false;

        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularDeflectionTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularMotorDecayTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularMotorTimescale = pValue;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_bankingEfficiency = pValue;
                    break;
                case Vehicle.BANKING_MIX:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_bankingMix = pValue;
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_bankingTimescale = pValue;
                    break;
                case Vehicle.BUOYANCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_VehicleBuoyancy = pValue;
                    break;
                case Vehicle.HOVER_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_VhoverEfficiency = pValue;
                    break;
                case Vehicle.HOVER_HEIGHT:
                    m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_VhoverTimescale = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearDeflectionTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearMotorDecayTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearMotorTimescale = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    if (pValue < 0.1f) pValue = 0.1f;    // Less goes unstable
                    if (pValue > 1.0f) pValue = 1.0f;
                    m_verticalAttractionEfficiency = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_verticalAttractionTimescale = pValue;
                    break;

                // These are vector properties but the engine lets you use a single float value to
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_angularMotorApply = 10;
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    break;

            }
        }//end ProcessFloatVehicleParam

        //All parts hooked up
        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue)
        {
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    if (m_angularMotorDirection.X > 12.56f) m_angularMotorDirection.X = 12.56f;
                    if (m_angularMotorDirection.X < -12.56f) m_angularMotorDirection.X = -12.56f;
                    if (m_angularMotorDirection.Y > 12.56f) m_angularMotorDirection.Y = 12.56f;
                    if (m_angularMotorDirection.Y < -12.56f) m_angularMotorDirection.Y = -12.56f;
                    if (m_angularMotorDirection.Z > 12.56f) m_angularMotorDirection.Z = 12.56f;
                    if (m_angularMotorDirection.Z < -12.56f) m_angularMotorDirection.Z = -12.56f;
                    m_angularMotorApply = 10;
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.BLOCK_EXIT:
                    m_BlockingEndPoint = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }
        }//end ProcessVectorVehicleParam

        //All parts hooked up
        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    m_referenceFrame = pValue;
                    break;
                case Vehicle.ROLL_FRAME:
                    m_RollreferenceFrame = pValue;
                    break;
            }
        }//end ProcessRotationVehicleParam

        internal void ProcessVehicleFlags(int pParam, bool remove)
        {
            if (remove)
            {
                if (pParam == -1)
                {
                    m_flags = (VehicleFlag)0;
                    m_Hoverflags = (VehicleFlag)0;
                    return;
                }
                else if ((pParam & (int)VehicleFlag.HOVER_GLOBAL_HEIGHT) == (int)VehicleFlag.HOVER_GLOBAL_HEIGHT)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_GLOBAL_HEIGHT);
                }
                else if ((pParam & (int)VehicleFlag.HOVER_TERRAIN_ONLY) == (int)VehicleFlag.HOVER_TERRAIN_ONLY)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_TERRAIN_ONLY) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY);
                }
                else if ((pParam & (int)VehicleFlag.HOVER_UP_ONLY) == (int)VehicleFlag.HOVER_UP_ONLY)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_UP_ONLY) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_UP_ONLY);
                }
                else if ((pParam & (int)VehicleFlag.HOVER_WATER_ONLY) == (int)VehicleFlag.HOVER_WATER_ONLY)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_WATER_ONLY) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY);
                }
                else if ((pParam & (int)VehicleFlag.LOCK_HOVER_HEIGHT) == (int)VehicleFlag.LOCK_HOVER_HEIGHT)
                {
                    if ((m_Hoverflags & VehicleFlag.LOCK_HOVER_HEIGHT) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.LOCK_HOVER_HEIGHT);
                }
                else if ((pParam & (int)VehicleFlag.LIMIT_MOTOR_UP) == (int)VehicleFlag.LIMIT_MOTOR_UP)
                {
                    if ((m_flags & VehicleFlag.LIMIT_MOTOR_UP) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.LIMIT_MOTOR_UP);
                }
                else if ((pParam & (int)VehicleFlag.LIMIT_ROLL_ONLY) == (int)VehicleFlag.LIMIT_ROLL_ONLY)
                {
                    if ((m_flags & VehicleFlag.LIMIT_ROLL_ONLY) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.LIMIT_ROLL_ONLY);
                }
                else if ((pParam & (int)VehicleFlag.MOUSELOOK_BANK) == (int)VehicleFlag.MOUSELOOK_BANK)
                {
                    if ((m_flags & VehicleFlag.MOUSELOOK_BANK) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.MOUSELOOK_BANK);
                }
                else if ((pParam & (int)VehicleFlag.MOUSELOOK_STEER) == (int)VehicleFlag.MOUSELOOK_STEER)
                {
                    if ((m_flags & VehicleFlag.MOUSELOOK_STEER) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.MOUSELOOK_STEER);
                }
                else if ((pParam & (int)VehicleFlag.NO_DEFLECTION_UP) == (int)VehicleFlag.NO_DEFLECTION_UP)
                {
                    if ((m_flags & VehicleFlag.NO_DEFLECTION_UP) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP);
                }
                else if ((pParam & (int)VehicleFlag.CAMERA_DECOUPLED) == (int)VehicleFlag.CAMERA_DECOUPLED)
                {
                    if ((m_flags & VehicleFlag.CAMERA_DECOUPLED) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.CAMERA_DECOUPLED);
                }
                else if ((pParam & (int)VehicleFlag.NO_X) == (int)VehicleFlag.NO_X)
                {
                    if ((m_flags & VehicleFlag.NO_X) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_X);
                }
                else if ((pParam & (int)VehicleFlag.NO_Y) == (int)VehicleFlag.NO_Y)
                {
                    if ((m_flags & VehicleFlag.NO_Y) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_Y);
                }
                else if ((pParam & (int)VehicleFlag.NO_Z) == (int)VehicleFlag.NO_Z)
                {
                    if ((m_flags & VehicleFlag.NO_Z) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_Z);
                }
                else if ((pParam & (int)VehicleFlag.NO_DEFLECTION) == (int)VehicleFlag.NO_DEFLECTION)
                {
                    if ((m_flags & VehicleFlag.NO_DEFLECTION) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_DEFLECTION);
                }
                else if ((pParam & (int)VehicleFlag.LOCK_ROTATION) == (int)VehicleFlag.LOCK_ROTATION)
                {
                    if ((m_flags & VehicleFlag.LOCK_ROTATION) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.LOCK_ROTATION);
                }
            }
            else
            {
                if ((pParam & (int)VehicleFlag.HOVER_GLOBAL_HEIGHT) == (int)VehicleFlag.HOVER_GLOBAL_HEIGHT)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_GLOBAL_HEIGHT | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.HOVER_TERRAIN_ONLY) == (int)VehicleFlag.HOVER_TERRAIN_ONLY)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_TERRAIN_ONLY | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.HOVER_UP_ONLY) == (int)VehicleFlag.HOVER_UP_ONLY)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_UP_ONLY | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.HOVER_WATER_ONLY) == (int)VehicleFlag.HOVER_WATER_ONLY)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_WATER_ONLY | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.LOCK_HOVER_HEIGHT) == (int)VehicleFlag.LOCK_HOVER_HEIGHT)
                {
                    m_Hoverflags |= (VehicleFlag.LOCK_HOVER_HEIGHT);
                }
                else if ((pParam & (int)VehicleFlag.LIMIT_MOTOR_UP) == (int)VehicleFlag.LIMIT_MOTOR_UP)
                {
                    m_flags |= (VehicleFlag.LIMIT_MOTOR_UP | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.MOUSELOOK_BANK) == (int)VehicleFlag.MOUSELOOK_BANK)
                {
                    m_flags |= (VehicleFlag.MOUSELOOK_BANK | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.MOUSELOOK_STEER) == (int)VehicleFlag.MOUSELOOK_STEER)
                {
                    m_flags |= (VehicleFlag.MOUSELOOK_STEER | m_flags);
                }
                if ((pParam & (int)VehicleFlag.NO_DEFLECTION_UP) == (int)VehicleFlag.NO_DEFLECTION_UP)
                {
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.CAMERA_DECOUPLED) == (int)VehicleFlag.CAMERA_DECOUPLED)
                {
                    m_flags |= (VehicleFlag.CAMERA_DECOUPLED | m_flags);
                }
                else if ((pParam & (int)VehicleFlag.NO_X) == (int)VehicleFlag.NO_X)
                {
                    m_flags |= (VehicleFlag.NO_X);
                }
                else if ((pParam & (int)VehicleFlag.NO_Y) == (int)VehicleFlag.NO_Y)
                {
                    m_flags |= (VehicleFlag.NO_Y);
                }
                else if ((pParam & (int)VehicleFlag.NO_Z) == (int)VehicleFlag.NO_Z)
                {
                    m_flags |= (VehicleFlag.NO_Z);
                }
                else if ((pParam & (int)VehicleFlag.NO_DEFLECTION) == (int)VehicleFlag.NO_DEFLECTION)
                {
                    m_flags |= (VehicleFlag.NO_DEFLECTION);
                }
                else if ((pParam & (int)VehicleFlag.LOCK_ROTATION) == (int)VehicleFlag.LOCK_ROTATION)
                {
                    m_flags |= (VehicleFlag.LOCK_ROTATION);
                }
            }
        }//end ProcessVehicleFlags

        internal void ProcessTypeChange(Vehicle pType)
        {
            // Set Defaults For Type
            m_type = pType;
            switch (pType)
            {
                case Vehicle.TYPE_NONE:
                    m_linearFrictionTimescale = new Vector3(0, 0, 0);
                    m_angularFrictionTimescale = new Vector3(0, 0, 0);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 0;
                    m_linearMotorDecayTimescale = 0;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 0;
                    m_angularMotorDecayTimescale = 0;
                    m_VhoverHeight = 0;
                    m_VhoverTimescale = 0;
                    m_VehicleBuoyancy = 0;
                    m_flags = (VehicleFlag)0;
                    m_referenceFrame = Quaternion.Identity;
                    break;

                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 120;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 1;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1;
                    m_angularDeflectionEfficiency = 1;
                    m_angularDeflectionTimescale = 1000;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 1;
                    m_bankingTimescale = 10;
                    m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &=
                         ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                           VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1;
                    m_angularMotorDecayTimescale = 0.8f;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 2;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 10;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 10f;
                    m_bankingEfficiency = -0.2f;
                    m_bankingMix = 1;
                    m_bankingTimescale = 1;
                    m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    m_Hoverflags |= (VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 2;
                    m_VehicleBuoyancy = 1;
                    m_linearDeflectionEfficiency = 0.5f;
                    m_linearDeflectionTimescale = 3;
                    m_angularDeflectionEfficiency = 0.5f;
                    m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 0.5f;
                    m_verticalAttractionTimescale = 5f;
                    m_bankingEfficiency = -0.3f;
                    m_bankingMix = 0.8f;
                    m_bankingTimescale = 1;
                    m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY |
                            VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.LIMIT_ROLL_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    m_Hoverflags |= (VehicleFlag.HOVER_WATER_ONLY);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 0.5f;
                    m_linearDeflectionTimescale = 3;
                    m_angularDeflectionEfficiency = 1;
                    m_angularDeflectionTimescale = 2;
                    m_verticalAttractionEfficiency = 0.9f;
                    m_verticalAttractionTimescale = 2f;
                    m_bankingEfficiency = 1;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 2;
                    m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 6;
                    m_angularMotorDecayTimescale = 10;
                    m_VhoverHeight = 5;
                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 1;
                    m_linearDeflectionEfficiency = 0;
                    m_linearDeflectionTimescale = 5;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 100f;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 5;
                    m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    m_Hoverflags |= (VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }
        }//end SetDefaultsForType

        internal void Enable(IntPtr pBody, AuroraODEPrim parent, AuroraODEPhysicsScene pParentScene)
        {
            if (m_enabled)
                return;
            m_enabled = true;
            m_previousMaterial = (Material)parent.m_material;
            parent.SetMaterial((int)Material.Glass); //This seems to happen in SL... and its needed for here
            parent.ThrottleUpdates = false;
            m_body = pBody;
            if (pBody == IntPtr.Zero || m_type == Vehicle.TYPE_NONE)
                return;

            d.Mass mass;
            d.BodyGetMass(pBody, out mass);

            Mass = mass.mass;
            Mass *= 2;
        }

        internal void Disable(AuroraODEPrim parent)
        {
            if (!m_enabled)
                return;
            m_enabled = false;

            parent.SetMaterial((int)m_previousMaterial); //Revert to the original

            parent.ThrottleUpdates = true;
            //d.BodyDisable(Body);
            m_linearMotorDirection = Vector3.Zero;
            m_linearMotorDirectionLASTSET = Vector3.Zero;
            m_angularMotorDirection = Vector3.Zero;
        }

        internal void Step(IntPtr pBody, float pTimestep, AuroraODEPhysicsScene pParentScene, AuroraODEPrim parent)
        {
            m_body = pBody;
            if (pBody == IntPtr.Zero || m_type == Vehicle.TYPE_NONE)
                return;
            if (!d.BodyIsEnabled(Body))
                d.BodyEnable(Body);

            frcount++;  // used to limit debug comment output
            if (frcount > 100)
                frcount = 0;

            MoveLinear(pTimestep, pParentScene);
            MoveAngular(pTimestep, pParentScene);
            LimitRotation(pTimestep);

            /*if (!parent.m_angularlock.ApproxEquals(Vector3.One, 0.003f) &&
                                parent.Amotor != IntPtr.Zero)
            {
                d.JointSetAMotorParam(Amotor, (int)dParam.LowStop, -0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.LoStop3, -0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.LoStop2, -0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop, 0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop3, 0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.HiStop2, 0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.Vel, 9000f);
                d.JointSetAMotorParam(Amotor, (int)dParam.FudgeFactor, 0f);
                d.JointSetAMotorParam(Amotor, (int)dParam.FMax, int.MaxValue);
                d.Vector3 avel2 = d.BodyGetAngularVel(Body);

                if (parent.m_angularlock.X == 0)
                    avel2.X = 0;
                if (parent.m_angularlock.Y == 0)
                    avel2.Y = 0;
                if (parent.m_angularlock.Z == 0)
                    avel2.Z = 0;
                d.BodySetAngularVel(Body, avel2.X, avel2.Y, avel2.Z);
                d.BodySetAngularDamping(Body, 1);
                d.BodySetTorque(Body, 0, 0, 0);
            }*/

            // WE deal with updates
            parent.RequestPhysicsterseUpdate();
        }   // end Step

        private void MoveLinear(float pTimestep, AuroraODEPhysicsScene _pParentScene)
        {
            d.Vector3 pos = d.BodyGetPosition (Body);
            d.Vector3 oldPos = pos;

            if (m_lastPositionVector.X != pos.X ||
                m_lastPositionVector.Y != pos.Y ||
                m_lastPositionVector.Z != pos.Z)
            {
                m_lastPositionVector = d.BodyGetPosition (Body);
                m_lastAngularVelocity = new Vector3 ((float)d.BodyGetAngularVel (Body).X, (float)d.BodyGetAngularVel (Body).Y, (float)d.BodyGetAngularVel (Body).Z);
            }
            if (!m_linearMotorDirection.ApproxEquals (Vector3.Zero, 0.01f))  // requested m_linearMotorDirection is significant
            {
                if (!d.BodyIsEnabled (Body))
                    d.BodyEnable (Body);

                // add drive to body
                Vector3 addAmount = m_linearMotorDirection / (m_linearMotorTimescale * m_linearMotorDecayTimescale / (pTimestep));
                
                m_lastLinearVelocityVector += (addAmount * 10);  // lastLinearVelocityVector is the current body velocity vector?

                // This will work temporarily, but we really need to compare speed on an axis
                // KF: Limit body velocity to applied velocity?
                if (Math.Abs(m_lastLinearVelocityVector.X) > Math.Abs(m_linearMotorDirectionLASTSET.X))
                    m_lastLinearVelocityVector.X = m_linearMotorDirectionLASTSET.X;
                if (Math.Abs(m_lastLinearVelocityVector.Y) > Math.Abs(m_linearMotorDirectionLASTSET.Y))
                    m_lastLinearVelocityVector.Y = m_linearMotorDirectionLASTSET.Y;
                if (Math.Abs(m_lastLinearVelocityVector.Z) > Math.Abs(m_linearMotorDirectionLASTSET.Z))
                    m_lastLinearVelocityVector.Z = m_linearMotorDirectionLASTSET.Z;
            }
            else
            {        // requested is not significant
                // if what remains of applied is small, zero it.
                if (m_lastLinearVelocityVector.ApproxEquals(Vector3.Zero, 0.01f))
                    m_lastLinearVelocityVector = Vector3.Zero;
            }
            m_linearMotorDirection = Vector3.Zero;
            // convert requested object velocity to world-referenced vector
            m_dir = m_lastLinearVelocityVector;
            d.Quaternion rot = d.BodyGetQuaternion (Body);
            Quaternion rotq = new Quaternion (rot.X, rot.Y, rot.Z, rot.W);    // rotq = rotation of object
            m_dir *= rotq;

            // Preserve the current Z velocity
            d.Vector3 vel_now = d.BodyGetLinearVel(Body);
            m_dir.Z += (float)vel_now.Z;        // Preserve the accumulated falling velocity

            #region Blocking End Points

            //This makes sure that the vehicle doesn't leave the defined limits of position
            if (m_BlockingEndPoint != Vector3.Zero)
            {
                Vector3 posChange = new Vector3();
                posChange.X = (float)(pos.X - m_lastPositionVector.X);
                posChange.Y = (float)(pos.Y - m_lastPositionVector.Y);
                posChange.Z = (float)(pos.Z - m_lastPositionVector.Z);

                if (pos.X >= (m_BlockingEndPoint.X - (float)1))
                    pos.X -= posChange.X + 1;

                if (pos.Y >= (m_BlockingEndPoint.Y - (float)1))
                    pos.Y -= posChange.Y + 1;

                if (pos.Z >= (m_BlockingEndPoint.Z - (float)1))
                    pos.Z -= posChange.Z + 1;

                if (pos.X <= 0)
                    pos.X += posChange.X + 1;

                if (pos.Y <= 0)
                    pos.Y += posChange.Y + 1;
            }

            #endregion

            // Check if hovering
            if ((m_Hoverflags & (VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT)) != 0)
            {
                // We should hover, get the target height
                if ((m_Hoverflags & VehicleFlag.HOVER_WATER_ONLY) != 0)
                {
                    m_VhoverTargetHeight = (float)_pParentScene.GetWaterLevel((float)pos.X, (float)pos.Y) + m_VhoverHeight;
                }
                if ((m_Hoverflags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0)
                {
                    m_VhoverTargetHeight = _pParentScene.GetTerrainHeightAtXY((float)pos.X, (float)pos.Y) + m_VhoverHeight;
                }
                if ((m_Hoverflags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != 0)
                {
                    m_VhoverTargetHeight = m_VhoverHeight;
                }

                if ((m_Hoverflags & VehicleFlag.HOVER_UP_ONLY) != 0)
                {
                    // If body is already heigher, use its height as target height
                    if (pos.Z > m_VhoverTargetHeight)
                        m_VhoverTargetHeight = (float)pos.Z;
                }

                if ((m_Hoverflags & VehicleFlag.LOCK_HOVER_HEIGHT) != 0)
                {
                    if ((pos.Z - m_VhoverTargetHeight) > .2 || (pos.Z - m_VhoverTargetHeight) < -.2)
                    {
                        if ((pos.Z - (pos.Z - m_VhoverTargetHeight)) >= _pParentScene.GetTerrainHeightAtXY((float)pos.X, (float)pos.Y))
                            pos.Z = m_VhoverTargetHeight;
                    }
                }
                else
                {
                    // m_VhoverEfficiency - 0=boucy, 1=Crit.damped
                    // m_VhoverTimescale - time to acheive height
                    float herr0 = (float)pos.Z - m_VhoverTargetHeight;
                    // Replace Vertical speed with correction figure if significant
                    if (Math.Abs(herr0) > 0.01f)
                    {
                        //Note: we use 1.05 because it doesn't disappear completely, only very critically damped
                        m_dir.Z = (float)((-((herr0 * pTimestep * 50.0f) / m_VhoverTimescale)) * (1.05 - m_VhoverEfficiency));
                    }
                    else
                        //Too small, zero it.
                        m_dir.Z = 0f;
                }
            }

            //Do this here, because it shouldn't clear out gravity or the tainted forces (not part of vehicle physics)
            if ((m_flags & (VehicleFlag.NO_X)) != 0)
                m_dir.X = 0;
            if ((m_flags & (VehicleFlag.NO_Y)) != 0)
                m_dir.Y = 0;
            if ((m_flags & (VehicleFlag.NO_Z)) != 0)
                m_dir.Z = 0;

            m_lastPositionVector = d.BodyGetPosition(Body);

            #region Deal with tainted forces

            // KF: So far I have found no good method to combine a script-requested
            // .Z velocity and gravity. Therefore only 0g will used script-requested
            // .Z velocity. >0g (m_VehicleBuoyancy < 1) will used modified gravity only.
            // m_VehicleBuoyancy: -1=2g; 0=1g; 1=0g;
            Vector3 TaintedForce = new Vector3();
            if (m_forcelist.Count != 0)
            {
                try
                {
                    for (int i = 0; i < m_forcelist.Count; i++)
                    {
                        TaintedForce = TaintedForce + (m_forcelist[i] * 100);
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    TaintedForce = Vector3.Zero;
                }
                catch (ArgumentOutOfRangeException)
                {
                    TaintedForce = Vector3.Zero;
                }
                m_forcelist = new List<Vector3>();
            }

            #endregion

            #region Deflection

            //Forward is the prefered direction
            Vector3 PreferredAxisOfMotion = new Vector3 (1 + 1 * (m_linearDeflectionEfficiency / m_linearDeflectionTimescale) * pTimestep * pTimestep * pTimestep, 1, 1);
            PreferredAxisOfMotion *= m_referenceFrame;

            //Multiply it so that it scales linearly
            m_dir *= PreferredAxisOfMotion;

            #endregion

            if (Mass == 0)
            {
                d.Mass mass;
                d.BodyGetMass(m_body, out mass);

                Mass = mass.mass;
                Mass *= 2;
            }
            //No Setting forces, only velocity! Forces are NOT recommended to be used by the ODE manual
            //d.BodySetForce(Body, _pParentScene.gravityx + TaintedForce.X,
            //    _pParentScene.gravityy + TaintedForce.Y,
            //    (_pParentScene.gravityz * Mass * (1f - m_VehicleBuoyancy)) + TaintedForce.Z);

            //d.BodySetForce(Body, _pParentScene.gravityx + TaintedForce.X + (m_dir.X * 10000),
            //    _pParentScene.gravityy + TaintedForce.Y + (m_dir.Y * 10000),
            //    (_pParentScene.gravityz * Mass * (1f - m_VehicleBuoyancy)) + TaintedForce.Z + (m_dir.Z * 10000));

            m_dir += TaintedForce;
            //Uses the square to make bouyancy more effective as in SL, as it seems to effect gravity more the higher the value is
            //This check helps keep things from being pushed into the ground and the consequence of being shoved back out
            m_dir.Z += ((_pParentScene.gravityz * (float)Mass) * ((((1 - m_VehicleBuoyancy) * (1 - m_VehicleBuoyancy))) * pTimestep));

            d.BodySetLinearVel(Body, m_dir.X, m_dir.Y, m_dir.Z);

            //Check for changes and only set it once
            if (pos.X != oldPos.X || pos.Y != oldPos.Y || pos.Z != oldPos.Z)
                d.BodySetPosition(Body, pos.X, pos.Y, pos.Z);

            // apply friction
            // note: seems more effective with how SL does this with the square
            Vector3 decayamount = Vector3.One / (m_linearFrictionTimescale / (pTimestep * pTimestep));
            m_lastLinearVelocityVector -= m_lastLinearVelocityVector * decayamount;
        } // end MoveLinear()

        private void MoveAngular(float pTimestep, AuroraODEPhysicsScene _pParentScene)
        {
            /*
            private Vector3 m_angularMotorDirection = Vector3.Zero;            // angular velocity requested by LSL motor
            private int m_angularMotorApply = 0;                            // application frame counter
             private float m_angularMotorVelocity = 0;                        // current angular motor velocity (ramps up and down)
            private float m_angularMotorTimescale = 0;                        // motor angular velocity ramp up rate
            private float m_angularMotorDecayTimescale = 0;                    // motor angular velocity decay rate
            private Vector3 m_angularFrictionTimescale = Vector3.Zero;        // body angular velocity  decay rate
            private Vector3 m_lastAngularVelocity = Vector3.Zero;            // what was last applied to body
            */

            // Get what the body is doing, this includes 'external' influences
            d.Vector3 angularVelocity = d.BodyGetAngularVel(Body);
            if ((m_flags & VehicleFlag.MOUSELOOK_STEER) == VehicleFlag.MOUSELOOK_STEER)
            {
                if (m_userLookAt != Vector3.Zero)
                {
                    /*m_lastCameraRotation = llRotBetween(new Vector3(d.BodyGetPosition(m_body).X, d.BodyGetPosition(m_body).Y, d.BodyGetPosition(m_body).Z), m_userLookAt);
                    m_lastCameraRotation *= 10;
                    Vector3 move = ToEuler(m_lastCameraRotation);
                    //move.Z *= (-1);
                    //move *= new Quaternion(d.BodyGetQuaternion(Body).X, d.BodyGetQuaternion(Body).Y, d.BodyGetQuaternion(Body).Z, d.BodyGetQuaternion(Body).W);
                    move.Z *= (float)(-2 * Math.PI);
                    move.Y = 0;
                    move.X = 0;
                    m_angularMotorVelocity += move / pTimestep;*/
                    m_userLookAt.Z = m_userLookAt.X * 10;
                    m_userLookAt.X = 0;
                    m_userLookAt.Y = 0;
                    m_angularMotorVelocity += m_userLookAt;
                    Console.WriteLine(m_userLookAt.Z);
                    //Console.WriteLine(move.Z);
                }
            }

            if (m_angularMotorApply > 0)
            {
                // ramp up to new value
                //   current velocity  +=                         error                       /    (time to get there / step interval)
                //                               requested speed            -  last motor speed
                //Add the mass, if the vehicle is attempting to turn, it does matter how much it weighs
                m_angularMotorVelocity.X += (m_angularMotorDirection.X - m_angularMotorVelocity.X) * (float)Mass /  (m_angularMotorTimescale / pTimestep);
                m_angularMotorVelocity.Y += (m_angularMotorDirection.Y - m_angularMotorVelocity.Y) * (float)Mass / (m_angularMotorTimescale / pTimestep);
                m_angularMotorVelocity.Z += (m_angularMotorDirection.Z - m_angularMotorVelocity.Z) * (float)Mass / (m_angularMotorTimescale / pTimestep);
                m_angularMotorApply--;        // This is done so that if script request rate is less than phys frame rate the expected
                // velocity may still be acheived.
            }
            else
            {
                m_angularMotorVelocity -= m_angularMotorVelocity / (m_angularMotorDecayTimescale / pTimestep);
            }
            if (m_angularMotorVelocity.ApproxEquals(Vector3.Zero, 0.01f))
                m_angularMotorVelocity = Vector3.Zero;

            d.Quaternion rot = d.BodyGetQuaternion(Body);

            Vector3 vertattr = Vector3.Zero;
            Vector3 bank = Vector3.Zero;
            Vector3 deflection = Vector3.Zero;

            #region Vertical attractor section

            if (m_verticalAttractionTimescale < 300)
            {
                Quaternion rotqq = new Quaternion((float)rot.X + m_referenceFrame.X,
                    (float)rot.Y + m_referenceFrame.Y,
                    (float)rot.Z + m_referenceFrame.Z,
                    (float)rot.W);    // rotq = rotation of object

                m_angularMotorVelocity *= rotqq;
                float VAservo = 0.2f / (m_verticalAttractionTimescale * pTimestep);
                // get present body rotation
                Quaternion rotq = new Quaternion((float)rot.X, (float)rot.Y, (float)rot.Z, (float)rot.W);
                // make a vector pointing up
                Vector3 verterr = Vector3.Zero;
                verterr.Z = 1.0f;
                // rotate it to Body Angle
                verterr = verterr * rotq;
                // verterr.X and .Y are the World error ammounts. They are 0 when there is no error (Vehicle Body is 'vertical'), and .Z will be 1.
                // As the body leans to its side |.X| will increase to 1 and .Z fall to 0. As body inverts |.X| will fall and .Z will go
                // negative. Similar for tilt and |.Y|. .X and .Y must be modulated to prevent a stable inverted body.
                if (verterr.Z < 0.0f)
                {
                    verterr.X = 2.0f - verterr.X;
                    verterr.Y = 2.0f - verterr.Y;
                }
                // Error is 0 (no error) to +/- 2 (max error)
                // scale it by VAservo
                verterr = verterr * VAservo;

                // As the body rotates around the X axis, then verterr.Y increases; Rotated around Y then .X increases, so
                // Change  Body angular velocity  X based on Y, and Y based on X. Z is not changed.
                vertattr.X = verterr.Y;
                vertattr.Y = -verterr.X;
                vertattr.Z = 0f;

                // scaling appears better using square-law
                float bounce = 1.0f - (m_verticalAttractionEfficiency * m_verticalAttractionEfficiency);
                vertattr.X += (float)(bounce * angularVelocity.X);
                vertattr.Y += (float)(bounce * angularVelocity.Y);

                #region Banking

                //X is the part that deals with banking

                // NOTES on banking  SEE http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial#Banking ----


                //VEHICLE_BANKING_EFFICIENCY - slider between -1 (leans out of turns),
                //    0 (no banking), and +1 (leans into turns)  m_bankingEfficiency

                //VEHICLE_BANKING_MIX - 	 slider between 0 (static banking)
                //    and 1 (dynamic banking)                    m_bankingMix

                //VEHICLE_BANKING_TIMESCALE - exponential timescale for the banking 
                //    behavior to take full effect               m_bankingTimescale


                // ----  END NOTES 

                /*//Banking cuts down on the X as it adds to the Z
                // Save this for later so we can reduce X as needed
                float oldAngularVelZ = m_angularMotorVelocity.Z;
                
                //Efficiency makes it go either inward or outward... so it can be multiplied by the X velocity 
                //  so that we get a rotation in the right direction with the right amount of force

                // Then divide by the timescale and timeStep so that we don't apply it all at once
                //if(m_angularMotorVelocity.Z > 0)
                if (m_angularMotorVelocity.X != 0)
                {
                }
                m_angularMotorVelocity.Z += (m_bankingEfficiency * m_angularMotorVelocity.X) / (m_bankingTimescale / pTimestep)*5;

                //Figure the difference of velocity transfered from X --> Z
                float angularVelZChange = m_angularMotorVelocity.Z - oldAngularVelZ;

                //Now remove the difference that came from Z since it was transfered from X --> Z
                
                /*if (m_angularMotorVelocity.X < 0)
                    m_angularMotorVelocity.X -= angularVelZChange / 3;
                else
                    m_angularMotorVelocity.X -= angularVelZChange / 3;*/
                
                float addAmount = m_angularMotorVelocity.X;

                m_angularMotorVelocity.Z += (((1 - m_bankingMix) * m_bankingEfficiency * addAmount) / (m_bankingTimescale / pTimestep) * 5);

                //float oldZAngVel = m_angularMotorVelocity.Z;
                //m_angularMotorVelocity.X -= oldZAngVel - m_angularMotorVelocity.Z;

                if (m_angularMotorVelocity.Z != 0)
                {
                }

                #endregion

            } // else vertical attractor is off

            //        m_lastVertAttractor = vertattr;
            #endregion

            #region Deflection

            //Forward is the prefered direction, but if the reference frame has changed, we need to take this into account as well
            Vector3 PreferredAxisOfMotion = new Vector3 (1 + (1 * (m_angularDeflectionEfficiency / m_angularDeflectionTimescale) * pTimestep * pTimestep * pTimestep), 1, 1);
            PreferredAxisOfMotion *= m_referenceFrame;

            //Multiply it so that it scales linearly
            deflection = PreferredAxisOfMotion;

            //deflection = ((PreferredAxisOfMotion * m_angularDeflectionEfficiency) / (m_angularDeflectionTimescale / pTimestep));

            #endregion

            // Sum of velocities
            m_lastAngularVelocity = m_angularMotorVelocity + vertattr + bank;
            m_lastAngularVelocity *= deflection;

            #region Limit Motor Up
            double Zchange = d.BodyGetLinearVel(Body).Z;

            if ((m_flags & (VehicleFlag.LIMIT_MOTOR_UP)) != 0 && Zchange > 0) //if it isn't going up, don't apply the limiting force
            {
                //Requires idea of 'up', so use reference frame to rotate it
                //Add to the X, because that will normally tilt the vehicle downward (if its rotated, it'll be rotated by the ref. frame
                m_lastAngularVelocity *= (new Vector3(1 - ((float)Zchange * (pTimestep * 10)), 1, 1) *  m_referenceFrame);
            }

            #endregion

            #region Block X,Y,Z rotation

            //Block off X,Y,Z rotation as requested
            if ((m_flags & (VehicleFlag.NO_X)) != 0)
                m_lastAngularVelocity.X = 0;
            if ((m_flags & (VehicleFlag.NO_Y)) != 0)
                m_lastAngularVelocity.Y = 0;
            if ((m_flags & (VehicleFlag.NO_Z)) != 0)
                m_lastAngularVelocity.Z = 0;

            #endregion

            if (m_lastAngularVelocity.ApproxEquals(Vector3.Zero, 0.01f))
                m_lastAngularVelocity = Vector3.Zero; // Reduce small value to zero.

            // apply friction
            Vector3 decayamount = Vector3.One / (m_angularFrictionTimescale / pTimestep);
            m_lastAngularVelocity -= m_lastAngularVelocity * decayamount;

            #region Linear Motor Offset

            //Offset section
            if (m_linearMotorOffset != Vector3.Zero)
            {
                //Offset of linear velocity doesn't change the linear velocity,
                //   but causes a torque to be applied, for example...
                //
                //      IIIII     >>>   IIIII
                //      IIIII     >>>    IIIII
                //      IIIII     >>>     IIIII
                //          ^
                //          |  Applying a force at the arrow will cause the object to move forward, but also rotate
                //
                //
                // The torque created is the linear velocity crossed with the offset

                //Note: we use the motor, otherwise you will just spin around and we divide by 10 since otherwise we go crazy
                Vector3 torqueFromOffset = (m_linearMotorDirectionLASTSET % m_linearMotorOffset) / 10;
                d.BodyAddTorque(Body, torqueFromOffset.X, torqueFromOffset.Y, torqueFromOffset.Z);
            }

            #endregion

            // Apply to the body
            //
            d.BodySetAngularVel(Body, m_lastAngularVelocity.X, m_lastAngularVelocity.Y, m_lastAngularVelocity.Z);
        }

        private Vector3 ToEuler(Quaternion m_lastCameraRotation)
        {
            Quaternion t = new Quaternion(m_lastCameraRotation.X * m_lastCameraRotation.X, m_lastCameraRotation.Y * m_lastCameraRotation.Y, m_lastCameraRotation.Z * m_lastCameraRotation.Z, m_lastCameraRotation.W * m_lastCameraRotation.W);
            double m = (m_lastCameraRotation.X + m_lastCameraRotation.Y + m_lastCameraRotation.Z + m_lastCameraRotation.W);
            if (m == 0) return Vector3.Zero;
            double n = 2 * (m_lastCameraRotation.Y * m_lastCameraRotation.W + m_lastCameraRotation.X * m_lastCameraRotation.Y);
            double p = m * m - n * n;
            if (p > 0)
                return new Vector3((float)NormalizeAngle(Math.Atan2(2.0 * (m_lastCameraRotation.X * m_lastCameraRotation.W - m_lastCameraRotation.Y * m_lastCameraRotation.Z), (-t.X - t.Y + t.Z + t.W))),
                                             (float)NormalizeAngle(Math.Atan2(n, Math.Sqrt(p))),
                                             (float)NormalizeAngle(Math.Atan2(2.0 * (m_lastCameraRotation.Z * m_lastCameraRotation.W - m_lastCameraRotation.X * m_lastCameraRotation.Y), (t.X - t.Y - t.Z + t.W))));
            else if (n > 0)
                return new Vector3(0, (float)(Math.PI * 0.5), (float)NormalizeAngle(Math.Atan2((m_lastCameraRotation.Z * m_lastCameraRotation.W + m_lastCameraRotation.X * m_lastCameraRotation.Y), 0.5 - t.X - t.Z)));
            else
                return new Vector3(0, (float)(-Math.PI * 0.5), (float)NormalizeAngle(Math.Atan2((m_lastCameraRotation.Z * m_lastCameraRotation.W + m_lastCameraRotation.X * m_lastCameraRotation.Y), 0.5 - t.X - t.Z)));
        }

        protected double NormalizeAngle(double angle)
        {
            if (angle > -Math.PI && angle < Math.PI)
                return angle;

            int numPis = (int)(Math.PI / angle);
            double remainder = angle - Math.PI * numPis;
            if (numPis % 2 == 1)
                return Math.PI - angle;
            return remainder;
        }

        //end MoveAngular

        internal void LimitRotation(float timestep)
        {
            if (m_RollreferenceFrame != Quaternion.Identity || (m_flags & VehicleFlag.LOCK_ROTATION) != 0)
            {
                d.Quaternion rot = d.BodyGetQuaternion(Body);
                d.Quaternion m_rot = rot;
                if (rot.X >= m_RollreferenceFrame.X)
                    m_rot.X = rot.X - (m_RollreferenceFrame.X / 2);

                if (rot.Y >= m_RollreferenceFrame.Y)
                    m_rot.Y = rot.Y - (m_RollreferenceFrame.Y / 2);

                if (rot.X <= -m_RollreferenceFrame.X)
                    m_rot.X = rot.X + (m_RollreferenceFrame.X / 2);

                if (rot.Y <= -m_RollreferenceFrame.Y)
                    m_rot.Y = rot.Y + (m_RollreferenceFrame.Y / 2);

                if ((m_flags & VehicleFlag.LOCK_ROTATION) != 0)
                {
                    m_rot.X = 0;
                    m_rot.Y = 0;
                }

                if (m_rot.X != rot.X || m_rot.Y != rot.Y || m_rot.Z != rot.Z)
                    d.BodySetQuaternion(Body, ref m_rot);
            }
        }

        private List<Vector3> m_forcelist = new List<Vector3>();
        //Quaternion m_lastCameraRotation = Quaternion.Identity;
        private Vector3 m_userLookAt = Vector3.Zero;
        internal void ProcessSetCameraPos(Vector3 CameraRotation)
        {
            //m_referenceFrame -= m_lastCameraRotation;
            //m_referenceFrame += CameraRotation;
            m_userLookAt = CameraRotation;
        }

        internal void ProcessForceTaint(Vector3 force)
        {
            m_forcelist.Add(force);
        }

        public Quaternion llRotBetween(Vector3 a, Vector3 b)
        {
            Quaternion rotBetween;
            // Check for zero vectors. If either is zero, return zero rotation. Otherwise,
            // continue calculation.
            if (a == Vector3.Zero || b == Vector3.Zero)
            {
                rotBetween = Quaternion.Identity;
            }
            else
            {
                a.Normalize();
                b.Normalize();
                double dotProduct = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
                // There are two degenerate cases possible. These are for vectors 180 or
                // 0 degrees apart. These have to be detected and handled individually.
                //
                // Check for vectors 180 degrees apart.
                // A dot product of -1 would mean the angle between vectors is 180 degrees.
                if (dotProduct < -0.9999999f)
                {
                    // First assume X axis is orthogonal to the vectors.
                    Vector3 orthoVector = new Vector3(1.0f, 0.0f, 0.0f);
                    orthoVector = orthoVector - a * (a.X / (a.X * a.X) + (a.Y * a.Y) + (a.Z * a.Z));
                    // Check for near zero vector. A very small non-zero number here will create
                    // a rotation in an undesired direction.
                    if (Math.Sqrt(orthoVector.X * orthoVector.X + orthoVector.Y * orthoVector.Y + orthoVector.Z * orthoVector.Z) > 0.0001)
                    {
                        rotBetween = new Quaternion(orthoVector.X, orthoVector.Y, orthoVector.Z, 0.0f);
                    }
                    // If the magnitude of the vector was near zero, then assume the X axis is not
                    // orthogonal and use the Z axis instead.
                    else
                    {
                        // Set 180 z rotation.
                        rotBetween = new Quaternion(0.0f, 0.0f, 1.0f, 0.0f);
                    }
                }
                // Check for parallel vectors.
                // A dot product of 1 would mean the angle between vectors is 0 degrees.
                else if (dotProduct > 0.9999999f)
                {
                    // Set zero rotation.
                    rotBetween = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
                }
                else
                {
                    // All special checks have been performed so get the axis of rotation.
                    Vector3 crossProduct = new Vector3
                    (
                    a.Y * b.Z - a.Z * b.Y,
                    a.Z * b.X - a.X * b.Z,
                    a.X * b.Y - a.Y * b.X
                    );
                    // Quarternion s value is the length of the unit vector + dot product.
                    double qs = 1.0 + dotProduct;
                    rotBetween = new Quaternion(crossProduct.X, crossProduct.Y, crossProduct.Z, (float)qs);
                    // Normalize the rotation.
                    double mag = Math.Sqrt(rotBetween.X * rotBetween.X + rotBetween.Y * rotBetween.Y + rotBetween.Z * rotBetween.Z + rotBetween.W * rotBetween.W);
                    // We shouldn't have to worry about a divide by zero here. The qs value will be
                    // non-zero because we already know if we're here, then the dotProduct is not -1 so
                    // qs will not be zero. Also, we've already handled the input vectors being zero so the
                    // crossProduct vector should also not be zero.
                    rotBetween.X = (float)(rotBetween.X / mag);
                    rotBetween.Y = (float)(rotBetween.Y / mag);
                    rotBetween.Z = (float)(rotBetween.Z / mag);
                    rotBetween.W = (float)(rotBetween.W / mag);
                    // Check for undefined values and set zero rotation if any found. This code might not actually be required
                    // any longer since zero vectors are checked for at the top.
                    if (Double.IsNaN(rotBetween.X) || Double.IsNaN(rotBetween.Y) || Double.IsNaN(rotBetween.Y) || Double.IsNaN(rotBetween.W))
                    {
                        rotBetween = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
                    }
                }
            }
            return rotBetween;
        }
    }
}
