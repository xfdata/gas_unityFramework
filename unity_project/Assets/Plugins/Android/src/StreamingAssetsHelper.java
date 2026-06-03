package com.helper.streamingassets;

import android.content.Context;
import android.os.Build;
import android.util.Log;

import java.io.File;
import java.nio.file.Files;
import java.nio.file.attribute.BasicFileAttributes;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class StreamingAssetsHelper {

    public static List<Map<String, Object>> getFilesInfoInAssets(Context context, String relativePath) {
        // 获取 StreamingAssets 路径
        String basePath = context.getApplicationInfo().nativeLibraryDir + "/../assets"; // assets 是 StreamingAssets 的路径
        if (relativePath != null && !relativePath.isEmpty()) {
            basePath += "/" + relativePath; // 添加相对路径
        }
        File directory = new File(basePath);

        // 检查目录是否存在
        if (!directory.exists()) {
            return null; // 返回 null 表示目录不存在
        }

        if (directory.isDirectory()) {
            List<Map<String, Object>> fileInfoList = new ArrayList<>();

            // 遍历文件
            File[] files = directory.listFiles();
            if (files != null) {
                for (File file : files) {
                    Map<String, Object> fileInfo = new HashMap<>();
                    fileInfo.put("name", file.getName()); // 文件名
                    fileInfo.put("isDirectory", file.isDirectory()); // 是否是文件夹
                    fileInfo.put("size", file.length()); // 文件大小
                    String relativeFilePath = file.getAbsolutePath().substring(basePath.length() + 1); // 获取相对路径
                    fileInfo.put("relativePath", relativeFilePath); // 文件相对路径

                    // 获取文件创建时间
                    long creationTime = 0L;
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                        try {
                            BasicFileAttributes attributes = Files.readAttributes(file.toPath(), BasicFileAttributes.class);
                            creationTime = attributes.creationTime().toMillis();
                        } catch (Exception e) {
                            Log.e("StreamingAssetsHelper", "Failed to retrieve creation time.", e);
                        }
                    } else {
                        // 对于低版本系统，无法直接获取创建时间，用最后修改时间近似代替
                        creationTime = file.lastModified();
                    }
                    fileInfo.put("creationTime", creationTime);

                    // 最后修改时间
                    fileInfo.put("lastModifiedTime", file.lastModified());

                    fileInfoList.add(fileInfo);
                }
            }
            return fileInfoList; // 返回文件信息列表
        }

        return null; // 如果不是目录，返回 null
    }

    // 新增查找函数：按通配符匹配文件，支持递归选项
    public static List<Map<String, Object>> findFilesInDirectory(Context context, String relativePath, String filePattern, boolean recursive) {
        // 获取 StreamingAssets 路径
        String basePath = context.getApplicationInfo().nativeLibraryDir + "/../assets"; // assets 是 StreamingAssets 的路径
        if (relativePath != null && !relativePath.isEmpty()) {
            basePath += "/" + relativePath; // 添加相对路径
        }
        File directory = new File(basePath);

        // 检查目录是否存在
        if (!directory.exists() || !directory.isDirectory()) {
            return null; // 返回 null 表示目录不存在或不是文件夹
        }

        // 文件匹配规则
        String regexPattern = convertWildcardToRegex(filePattern);

        List<Map<String, Object>> matchedFiles = new ArrayList<>();
        findFiles(directory, regexPattern, recursive, matchedFiles, basePath);
        return matchedFiles; // 返回匹配到的文件信息
    }

    // 递归查找文件的工具方法
    private static void findFiles(File directory, String regexPattern, boolean recursive, List<Map<String, Object>> matchedFiles, String basePath) {
        File[] files = directory.listFiles();
        if (files != null) {
            for (File file : files) {
                if (file.isDirectory() && recursive) {
                    findFiles(file, regexPattern, true, matchedFiles, basePath); // 递归查找子目录
                } else if (file.getName().matches(regexPattern)) {
                    Map<String, Object> fileInfo = new HashMap<>();
                    fileInfo.put("name", file.getName()); // 文件名
                    String relativeFilePath = file.getAbsolutePath().substring(basePath.length() + 1); // 获取相对路径
                    fileInfo.put("relativePath", relativeFilePath); // 文件相对路径
                    fileInfo.put("isDirectory", file.isDirectory()); // 是否是文件夹
                    fileInfo.put("size", file.length()); // 文件大小
                    fileInfo.put("lastModifiedTime", file.lastModified()); // 最后修改时间
                    matchedFiles.add(fileInfo);
                }
            }
        }
    }

    // 工具方法：将通配符转换为正则表达式
    private static String convertWildcardToRegex(String wildcard) {
        if (wildcard == null || wildcard.isEmpty()) {
            return ".*"; // 默认匹配所有文件
        }
        return wildcard.replace(".", "\\.").replace("*", ".*").replace("?", ".");
    }
}
