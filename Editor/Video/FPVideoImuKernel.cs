// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>Double-precision, source-clock X6 fusion. Gyro supplies motion; gated gravity corrects tilt only.</summary>
    internal static class FPVideoImuKernel
    {
        private const double Pi = 3.1415926535897932384626433832795;
        internal static double4 Multiply(double4 a, double4 b) => new double4(a.w*b.xyz+b.w*a.xyz+math.cross(a.xyz,b.xyz),a.w*b.w-math.dot(a.xyz,b.xyz));
        internal static double4 Normalize(double4 q) => q / math.sqrt(math.dot(q,q));
        internal static double3 Rotate(double4 q,double3 v) => v+2*math.cross(q.xyz,math.cross(q.xyz,v)+q.w*v);
        internal static double4 Mix(double4 a,double4 b,double t) => Normalize(a*(1-t)+(math.dot(a,b)<0?-b:b)*t);
        internal static double4 Step(double3 radians)
        {
            double angle=math.length(radians);
            return angle<1e-12 ? Normalize(new double4(radians*.5,1)) : new double4(radians*(math.sin(angle*.5)/angle),math.cos(angle*.5));
        }
        internal static double4 FromTo(double3 from,double3 to)
        {
            double dot=math.clamp(math.dot(from,to),-1,1);
            if(dot < -.999999) { double3 axis=math.cross(from,new double3(1,0,0));if(math.lengthsq(axis)<1e-6)axis=math.cross(from,new double3(0,0,1));return new double4(math.normalize(axis),0); }
            return Normalize(new double4(math.cross(from,to),1+dot));
        }
        [BurstCompile(FloatMode=FloatMode.Strict,FloatPrecision=FloatPrecision.High)]
        internal struct Fuse : IJob
        {
            [ReadOnly] public NativeArray<byte> Bytes;
            public NativeArray<double4> Poses;
            public NativeArray<double> Times, Stats;
            public NativeArray<int> Result;
            public long EpochMicros;
            public double OffsetMs, AccelerationRange, GyroRange;
            public int Stride;
            private ulong Timestamp(int p)
            {
                ulong v=0;for(int j=0;j<8;j++)v|=(ulong)Bytes[p+j]<<(j*8);return v;
            }
            private double Count(int p) => (Bytes[p]|Bytes[p+1]<<8)-32768;
            public void Execute()
            {
                int count=Bytes.Length/20,output=0,accepted=0;double lastTime=0,minDt=double.MaxValue,maxDt=0;
                double3 previousGyro=0;double4 q=new double4(0,0,0,1);bool initialized=false;
                for(int i=0;i<count;i++)
                {
                    int p=i*20;ulong timestamp=Timestamp(p);
                    if(timestamp>9000000000000000ul){Result[0]=1;return;}
                    double time=((long)timestamp-EpochMicros)*1e-6-OffsetMs*.001;
                    double3 a=new double3(Count(p+8),Count(p+10),Count(p+12))*(AccelerationRange/32768);
                    double3 g=new double3(Count(p+14),Count(p+16),Count(p+18));
                    if(math.cmax(math.abs(g))>=32760){Result[0]=3;return;}
                    // X6 / reversed tracks: image-motion-qualified axial map; acceleration supplies up with opposite sign.
                    double3 gyro=new double3(-g.z,g.x,-g.y)*(GyroRange/32768*Pi/180);
                    double3 up=new double3(a.z,-a.x,a.y);double norm=math.length(up);
                    bool gravity=norm>=.85&&norm<=1.15;
                    if(gravity&&!initialized){q=FromTo(up/norm,new double3(0,1,0));initialized=true;Stats[3]=time;}
                    if(i>0)
                    {
                        double dt=time-lastTime;
                        if(dt<=0||dt>.05){Result[0]=2;return;}
                        minDt=math.min(minDt,dt);maxDt=math.max(maxDt,dt);
                        q=Normalize(Multiply(q,Step((gyro+previousGyro)*(.5*dt))));
                        if(gravity&&initialized)
                        {
                            double3 worldUp=Rotate(q,up/norm);
                            if(math.dot(worldUp,new double3(0,1,0))>math.cos(20*Pi/180))
                            {
                                double4 correction=FromTo(worldUp,new double3(0,1,0));
                                q=Normalize(Multiply(Mix(new double4(0,0,0,1),correction,1-math.exp(-dt/5)),q));accepted++;
                            }
                        }
                    }
                    if(i%Stride==0||i==count-1){Poses[output]=q;Times[output]=time;output++;}
                    lastTime=time;previousGyro=gyro;
                }
                if(!initialized){Result[0]=4;return;}
                Result[1]=output;Stats[0]=minDt;Stats[1]=maxDt;Stats[2]=accepted;
            }
        }
        [BurstCompile(FloatMode=FloatMode.Strict,FloatPrecision=FloatPrecision.High)]
        internal struct Smooth : IJob
        {
            [ReadOnly] public NativeArray<double4> Raw;
            [ReadOnly] public NativeArray<double> Times;
            public NativeArray<double4> Filtered;
            public double Seconds;
            public void Execute()
            {
                Filtered[0]=Raw[0];
                for(int i=1;i<Raw.Length;i++)Filtered[i]=Mix(Filtered[i-1],Raw[i],1-math.exp(-(Times[i]-Times[i-1])/Seconds));
                for(int i=Raw.Length-2;i>=0;i--)Filtered[i]=Mix(Filtered[i+1],Filtered[i],1-math.exp(-(Times[i+1]-Times[i])/Seconds));
            }
        }
    }
}
