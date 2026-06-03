package com.videolayer;


import android.opengl.GLES20;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;


final class GLUtil {
    private static final FloatBuffer POS_BUF;
    private static final FloatBuffer UV_BUF;


    static {
        float[] pos = {
                -0.5f, -0.5f,
                0.5f, -0.5f,
                -0.5f, 0.5f,
                0.5f, 0.5f,
        };
        float[] uv = {
                0f, 0f,
                1f, 0f,
                0f, 1f,
                1f, 1f,
        };

        ByteBuffer pb = ByteBuffer.allocateDirect(pos.length * 4).order(ByteOrder.nativeOrder());
        POS_BUF = pb.asFloatBuffer();
        POS_BUF.put(pos).position(0);

        ByteBuffer ub = ByteBuffer.allocateDirect(uv.length * 4).order(ByteOrder.nativeOrder());
        UV_BUF = ub.asFloatBuffer();
        UV_BUF.put(uv).position(0);
    }


static int createShader(int type, String src) {
int sh = GLES20.glCreateShader(type);
GLES20.glShaderSource(sh, src);
GLES20.glCompileShader(sh);
int[] ok = new int[1];
GLES20.glGetShaderiv(sh, GLES20.GL_COMPILE_STATUS, ok, 0);
if (ok[0] == 0) {
String log = GLES20.glGetShaderInfoLog(sh);
GLES20.glDeleteShader(sh);
throw new RuntimeException("Shader compile error: " + log);
}
return sh;
}


static int createProgram(String vs, String fs) {
int v = createShader(GLES20.GL_VERTEX_SHADER, vs);
int f = createShader(GLES20.GL_FRAGMENT_SHADER, fs);
int p = GLES20.glCreateProgram();
GLES20.glAttachShader(p, v);
GLES20.glAttachShader(p, f);
GLES20.glLinkProgram(p);
int[] ok = new int[1];
GLES20.glGetProgramiv(p, GLES20.GL_LINK_STATUS, ok, 0);
if (ok[0] == 0) {
String log = GLES20.glGetProgramInfoLog(p);
GLES20.glDeleteProgram(p);
throw new RuntimeException("Program link error: " + log);
}
GLES20.glDeleteShader(v);
GLES20.glDeleteShader(f);
return p;
}


static void drawFullscreenQuad(int posLoc, int uvLoc, boolean flipY) {
    GLES20.glEnableVertexAttribArray(posLoc);
    GLES20.glEnableVertexAttribArray(uvLoc);
    GLES20.glVertexAttribPointer(posLoc, 2, GLES20.GL_FLOAT, false, 0, POS_BUF);
    GLES20.glVertexAttribPointer(uvLoc, 2, GLES20.GL_FLOAT, false, 0, UV_BUF);
    GLES20.glDrawArrays(GLES20.GL_TRIANGLE_STRIP, 0, 4);
    GLES20.glDisableVertexAttribArray(posLoc);
    GLES20.glDisableVertexAttribArray(uvLoc);
}
}