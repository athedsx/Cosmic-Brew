# Monta o trailer: quadros JPG do TrailerDirector + música lo-fi do jogo -> MP4 (H.264 + AAC).
# Uso: python Tools/trailer/make_trailer.py [pasta_quadros] [saida.mp4]
# Precisa do ffmpeg portátil: pip install imageio-ffmpeg
import os, subprocess, sys
import imageio_ffmpeg

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
frames = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Trailer", "frames")
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "Trailer", "CosmicBrew_Trailer.mp4")
music = os.path.join(ROOT, "Assets", "Audio", "Music", "lofi_01_cafe_na_nebulosa.wav")
FPS = 30

count = len([f for f in os.listdir(frames) if f.endswith(".jpg")])
dur = count / FPS
fade_out = 2.5
ff = imageio_ffmpeg.get_ffmpeg_exe()
cmd = [
    ff, "-y",
    "-framerate", str(FPS), "-i", os.path.join(frames, "f%05d.jpg"),
    "-stream_loop", "-1", "-i", music,
    "-filter_complex",
    f"[1:a]atrim=0:{dur:.3f},afade=t=in:st=0:d=1.2,afade=t=out:st={dur - fade_out:.3f}:d={fade_out},volume=0.9[a]",
    "-map", "0:v", "-map", "[a]",
    "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
    "-c:a", "aac", "-b:a", "192k", "-shortest", out,
]
print(f"{count} quadros ({dur:.1f} s) -> {out}")
subprocess.run(cmd, check=True)
print("pronto:", out)
