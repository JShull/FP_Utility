// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>Exploratory image-boundary coverage, not an optical or vendor-mask measurement.</summary>
    internal sealed class FPVideoOverlapAnalysis
    {
        internal struct Boundary { internal double X, Y, Radius, Residual; internal int Evidence; }
        internal readonly Boundary[] Boundaries = new Boundary[2];
        internal readonly double[] MinimumFov = new double[2];
        internal readonly double[] MaximumFov = new double[2];
        internal readonly List<double> EquatorialBands = new List<double>();
        internal double OverlapPercent, MissingPercent;
        internal Color32[] Map;
        internal const int MapWidth=360, MapHeight=181;

        internal static Boundary DetectBoundary(Color32[] image,int width,int height,int track)
        {
            if (image == null || width != height*2 || image.Length != width*height || height < 128 || track < 0 || track > 1)
                throw new ArgumentException("Expected two square lens images of at least 128 pixels each.");
            var points = new List<Vector2>(); var radii = new List<float>();
            int bins=0;
            for(int a=0;a<360;a++)
            {
                double angle=a*Math.PI/180, dx=Math.Cos(angle),dy=Math.Sin(angle); int last=-1,inside=0;
                // Locate the outermost reliably non-black sample; only accept rays with visible dark exterior.
                for(int k=(int)(height*.36);k<height*.71;k++)
                {
                    int x=(int)Math.Round(height*.5+dx*k),y=(int)Math.Round(height*.5+dy*k);
                    if(x<0||x>=height||y<0||y>=height) break;
                    var c=image[y*width+track*height+x];
                    if(Math.Max(c.r,Math.Max(c.g,c.b))>12) { last=k; inside++; }
                }
                if(last<height*.38 || last>height*.55 || inside<height*.035)continue;
                bool dark=true;
                for(int k=last+1;k<=last+Math.Max(3,height/100);k++)
                {
                    int x=(int)Math.Round(height*.5+dx*k),y=(int)Math.Round(height*.5+dy*k);
                    if(x<0||x>=height||y<0||y>=height) { dark=false; break; }
                    var c=image[y*width+track*height+x];
                    if(Math.Max(c.r,Math.Max(c.g,c.b))>12) { dark=false; break; }
                }
                if(!dark)continue;
                points.Add(new Vector2((float)(.5+dx*last/height),(float)(.5+dy*last/height)));
                radii.Add((float)last/height); bins |= 1 << (a/45);
            }
            int covered=0;for(int i=0;i<8;i++)if((bins&(1<<i))!=0)covered++;
            if(points.Count<40 || covered<6) throw new InvalidOperationException($"Lens {track+1}: not enough visible image boundary; try a brighter recording or different frames.");
            radii.Sort(); double median=radii[radii.Count/2];
            // Reject texture/occlusion edges that are far from the dominant circular boundary.
            double xx=0,xy=0,x1=0,yy=0,y1=0,bx=0,by=0,b1=0; int n=0,inlierBins=0;
            foreach(var p in points)
            {
                double r=Math.Sqrt((p.x-.5)*(p.x-.5)+(p.y-.5)*(p.y-.5)); if(Math.Abs(r-median)>.025)continue;
                double x=p.x,y=p.y,z=-(x*x+y*y); xx+=x*x;xy+=x*y;x1+=x;yy+=y*y;y1+=y;bx+=x*z;by+=y*z;b1+=z;n++;
                int az=(int)((Math.Atan2(y-.5,x-.5)*180/Math.PI+360)%360);inlierBins|=1<<(az/45);
            }
            int inlierSectors=0;for(int i=0;i<8;i++)if((inlierBins&(1<<i))!=0)inlierSectors++;
            if(n<40||inlierSectors<6)throw new InvalidOperationException($"Lens {track+1}: boundary evidence is not distributed around the lens.");
            double[,] m={{xx,xy,x1,bx},{xy,yy,y1,by},{x1,y1,n,b1}};
            for(int col=0;col<3;col++)
            {
                int pivot=col; for(int row=col+1;row<3;row++)if(Math.Abs(m[row,col])>Math.Abs(m[pivot,col]))pivot=row;
                if(Math.Abs(m[pivot,col])<1e-10)throw new InvalidOperationException("Lens boundary fit is degenerate.");
                for(int j=col;j<4;j++){double t=m[col,j];m[col,j]=m[pivot,j];m[pivot,j]=t;}
                double d=m[col,col];for(int j=col;j<4;j++)m[col,j]/=d;
                for(int row=0;row<3;row++)if(row!=col){double f=m[row,col];for(int j=col;j<4;j++)m[row,j]-=f*m[col,j];}
            }
            double cx=-m[0,3]/2,cy=-m[1,3]/2,rr=cx*cx+cy*cy-m[2,3];
            if(rr<=0)throw new InvalidOperationException("Lens boundary fit is invalid.");
            double radius=Math.Sqrt(rr),sum=0;int used=0;
            foreach(var p in points){double r=Math.Sqrt((p.x-cx)*(p.x-cx)+(p.y-cy)*(p.y-cy));if(Math.Abs(r-radius)<.025){sum+=(r-radius)*(r-radius);used++;}}
            double residual=Math.Sqrt(sum/Math.Max(1,used));
            if(used<40 || radius<.38 || radius>.55 || Math.Abs(cx-.5)>.08 || Math.Abs(cy-.5)>.08 || residual>.012)
                throw new InvalidOperationException($"Lens {track+1}: detected image edge is not a reliable circle.");
            return new Boundary { X=cx,Y=cy,Radius=radius,Residual=residual,Evidence=used };
        }

        internal static FPVideoOverlapAnalysis Calculate(FPVideoFactoryCalibration model,Color32[] image,int width,int height,double margin)
        {
            if(model==null)throw new ArgumentNullException(nameof(model)); model.Validate();
            if(double.IsNaN(margin)||double.IsInfinity(margin)||margin<0||margin>.05)throw new ArgumentOutOfRangeException(nameof(margin));
            var result=new FPVideoOverlapAnalysis(); var limits=new double[2][]; var matrices=new Matrix4x4[2];
            for(int track=0;track<2;track++)
            {
                var boundary=DetectBoundary(image,width,height,track); result.Boundaries[track]=boundary;
                double usable=boundary.Radius-margin;if(usable<=0)throw new InvalidOperationException("Safety margin exceeds the detected lens radius.");
                limits[track]=new double[360]; matrices[track]=model.WorldToLens(track);
                for(int az=0;az<360;az++)
                {
                    double phi=az*Math.PI/180,lo=0,hi=120; bool crossed=false;
                    for(double theta=.5;theta<=120;theta+=.5)
                    {
                        double t=theta*Math.PI/180;var local=new Vector3((float)(Math.Sin(t)*Math.Cos(phi)),(float)(Math.Sin(t)*Math.Sin(phi)),(float)Math.Cos(t));
                        Vector2 uv=model.ProjectCameraRay(local,track); double x=uv.x*2-track,y=uv.y;
                        if(x<0||x>1||y<0||y>1||(x-boundary.X)*(x-boundary.X)+(y-boundary.Y)*(y-boundary.Y)>usable*usable)
                        { hi=theta;lo=theta-.5;crossed=true;break; }
                    }
                    if(!crossed)throw new InvalidOperationException("Projected boundary did not close within 120° of the lens axis.");
                    for(int i=0;i<9;i++)
                    {
                        double mid=(lo+hi)*.5,t=mid*Math.PI/180;var local=new Vector3((float)(Math.Sin(t)*Math.Cos(phi)),(float)(Math.Sin(t)*Math.Sin(phi)),(float)Math.Cos(t));
                        Vector2 uv=model.ProjectCameraRay(local,track);double x=uv.x*2-track,y=uv.y;
                        if(x<0||x>1||y<0||y>1||(x-boundary.X)*(x-boundary.X)+(y-boundary.Y)*(y-boundary.Y)>usable*usable)hi=mid;else lo=mid;
                    }
                    limits[track][az]=lo;
                }
                double min=360,max=0;
                for(int az=0;az<180;az++)
                {
                    double full=limits[track][az]+limits[track][az+180];
                    min=Math.Min(min,full);max=Math.Max(max,full);
                }
                result.MinimumFov[track]=min; result.MaximumFov[track]=max;
            }
            bool Covered(Vector3 ray,int track)
            {
                Vector3 v=matrices[track].MultiplyVector(ray).normalized;
                double angle=Math.Acos(Math.Max(-1,Math.Min(1,v.z)))*180/Math.PI;
                int az=(int)Math.Round((Math.Atan2(v.y,v.x)*180/Math.PI+360)%360)%360;
                return angle<=limits[track][az];
            }
            result.Map=new Color32[MapWidth*MapHeight];double overlap=0,missing=0,total=0;
            for(int y=0;y<MapHeight;y++)
            {
                double lat=(-90+(y+.5)*180/MapHeight)*Math.PI/180,weight=Math.Cos(lat);
                for(int x=0;x<MapWidth;x++)
                {
                    double lon=(-180+(x+.5)*360/MapWidth)*Math.PI/180;
                    var ray=new Vector3((float)(Math.Sin(lon)*Math.Cos(lat)),(float)Math.Sin(lat),(float)(Math.Cos(lon)*Math.Cos(lat)));
                    bool a=Covered(ray,0),b=Covered(ray,1);total+=weight;if(a&&b)overlap+=weight;if(!a&&!b)missing+=weight;
                    result.Map[y*MapWidth+x]=a&&b?new Color32(240,240,240,255):a?new Color32(32,180,180,255):b?new Color32(190,50,190,255):new Color32(10,10,10,255);
                }
            }
            result.OverlapPercent=100*overlap/total;result.MissingPercent=100*missing/total;
            // Circular one-degree samples; report each connected equatorial overlap band.
            var equator=new bool[360];for(int x=0;x<360;x++) { double lon=(-180+x)*Math.PI/180;var ray=new Vector3((float)Math.Sin(lon),0,(float)Math.Cos(lon));equator[x]=Covered(ray,0)&&Covered(ray,1); }
            int start=Array.FindIndex(equator,v=>!v);
            if(start<0)result.EquatorialBands.Add(360);
            else { int count=0;for(int i=1;i<=360;i++){if(equator[(start+i)%360])count++;else if(count>0){result.EquatorialBands.Add(count);count=0;}} }
            result.EquatorialBands.Sort();result.EquatorialBands.Reverse(); return result;
        }
    }

    /// <summary>Samples three source times using an isolated decoder; never changes the window playhead.</summary>
    internal sealed class FPVideoOverlapSampler : IDisposable
    {
        private readonly FPVideoNativeDecoder decoder;
        private readonly Texture2D atlas, readback;
        private readonly RenderTexture target;
        private readonly FPVideoFactoryCalibration model;
        private readonly double duration, margin;
        private readonly Color32[] maximum;
        private int sample;
        private bool seeking;
        private int prerollFrames;
        private double sampleTime;
        internal FPVideoOverlapAnalysis Result { get; private set; }
        internal int Samples => sample;
        internal bool Finished => Result != null;
        internal FPVideoOverlapSampler(FPVideoNativeSource source,FPVideoFactoryCalibration calibration,double margin)
        {
            if(source.SecondDecodePath==null || !source.Ready.IsCompletedSuccessfully)throw new InvalidOperationException("Wait for the paired source to finish staging.");
            model=calibration.Snapshot();duration=source.Info.Duration;this.margin=margin;
            decoder=new FPVideoNativeDecoder(source.DecodePath,source.SecondDecodePath);
            try
            {
                atlas=new Texture2D(1024,512,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave };
                readback=new Texture2D(1024,512,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave };
                target=new RenderTexture(1024,512,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { hideFlags=HideFlags.HideAndDontSave };
                if(!target.Create())throw new InvalidOperationException("Could not allocate overlap analysis texture.");
                maximum=new Color32[1024*512];
            }
            catch { Dispose();throw; }
        }
        internal void Tick()
        {
            if(Finished)return;
            if(sample<3)
            {
                if(!seeking)
                {
                    sampleTime=duration*(sample==0?.15:sample==1?.5:.85);
                    decoder.Seek(sampleTime);seeking=true;prerollFrames=0;return;
                }
                if(!decoder.Read(atlas,out double pts))throw new InvalidOperationException("No decoded frame at the selected overlap sample time.");
                if(pts+.000001<sampleTime)
                {
                    if(++prerollFrames>512)throw new InvalidOperationException("Overlap sample seek exceeded 512 preroll frames.");
                    return;
                }
                var old=RenderTexture.active;
                try { Graphics.Blit(atlas,target);RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,1024,512),0,0);readback.Apply(false,false); }
                finally { RenderTexture.active=old; }
                var pixels=readback.GetPixels32();
                for(int i=0;i<pixels.Length;i++)
                {
                    var p=pixels[i];var q=maximum[i];
                    maximum[i]=new Color32(Math.Max(p.r,q.r),Math.Max(p.g,q.g),Math.Max(p.b,q.b),255);
                }
                sample++;seeking=false;return;
            }
            Result=FPVideoOverlapAnalysis.Calculate(model,maximum,1024,512,margin);
        }
        public void Dispose()
        {
            decoder?.Dispose();if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            if(atlas!=null)UnityEngine.Object.DestroyImmediate(atlas);if(readback!=null)UnityEngine.Object.DestroyImmediate(readback);
        }
    }
}
