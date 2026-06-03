#import <Foundation/Foundation.h>
#import <sys/statvfs.h>
#include "UnityAppController.h"
//#import <CoreTelephony/CTTelephonyNetworkInfo.h>
//#import <CoreTelephony/CTCarrier.h>
#import <UIKit/UIKit.h>
#import <Photos/Photos.h>
#import <AVFoundation/AVFoundation.h>

extern "C"
{
    void setClipData(const char* message)
    {
       UIPasteboard *pasteboard = [UIPasteboard generalPasteboard];
       NSString *string1 = [[NSString alloc] initWithUTF8String:message];
       NSLog(@"Unity :%@",string1);
       pasteboard.string = string1;
    }

    NSString* getClipData()
    {
        UIPasteboard *pasteboard = [UIPasteboard generalPasteboard];
        return pasteboard.string;
    }

    //获取安全距离
    const void getSafeAreaInsets(int* left, int* right, int* top, int* down)
    {
        float statusBarHeight = 0;
        // if(@available(iOS 13.0, *))
        // {
        //     UIStatusBarManager *statusBarManager = [UIApplication sharedApplication].windows.firstObject.windowScene.statusBarManager;
        //     statusBarHeight = statusBarManager.statusBarFrame.size.height;
        // }
        // else
        // {
        //     statusBarHeight = [UIApplication sharedApplication].statusBarFrame.size.height;
        // }

        *left = 0;
        *right = 0;
        *top = 0;
        *down = 0;
        if(@available(iOS 11.0, *))
        {
            CGFloat scale = [UIScreen mainScreen].scale;
            UIEdgeInsets safeAreaInset = UIApplication.sharedApplication.windows[0].safeAreaInsets;
            if(safeAreaInset.bottom > 0 && [[UIDevice currentDevice].model isEqualToString:@"iPhone"])
            {
                statusBarHeight = 44;
                UIInterfaceOrientation orientation = [UIApplication sharedApplication].statusBarOrientation;
                switch (orientation) {
                    case UIInterfaceOrientationPortrait:
                        *top = (int)((statusBarHeight - 12) * scale);
                        break;
                    case UIInterfaceOrientationLandscapeLeft:
                        NSLog(@"界面朝左");
                        *left = (int)((statusBarHeight - 12) * scale);
                        *right = (int)(safeAreaInset.right * scale);
                        break;
                    case UIInterfaceOrientationLandscapeRight:
                        NSLog(@"界面朝右");
                        *left = (int)(safeAreaInset.right * scale);
                        *right = (int)((statusBarHeight - 12) * scale);
                        break;
                    default:
                        break;
                }
            }
        }
      
    }

    bool isAvailableIOS11()
    {
        return @available(iOS 11.0, *);
    }

    long GetFreeDiskSpace() {
        NSFileManager *fileManager = [NSFileManager defaultManager];
        NSDictionary *attributes = [fileManager attributesOfFileSystemForPath:NSHomeDirectory() error:nil];
        if (attributes) {
            NSNumber *freeSize = attributes[NSFileSystemFreeSize];
            return [freeSize longValue];
        }
        return -1;
    }

    //获取运营商code
    const char* getCarrier() {
        /*if (@available(iOS 12.0, *)) {
            CTTelephonyNetworkInfo *info = [[CTTelephonyNetworkInfo alloc] init];
            if (!info) return strdup("[no Carrier]");
            
            NSDictionary<NSString *, CTCarrier *> *carriers = [info serviceSubscriberCellularProviders];
            if (!carriers || carriers.count == 0) return strdup("[no Carrier]");
            
            CTCarrier *carrier = carriers[carriers.allKeys.firstObject];
            if (!carrier.mobileCountryCode || !carrier.mobileNetworkCode) {
                return strdup("[no Carrier]");
            }
            
            NSString *code = [NSString stringWithFormat:@"%@%@", 
                            carrier.mobileCountryCode, 
                            carrier.mobileNetworkCode];
            return strdup(code.UTF8String);
        }*/
        return strdup("[no Carrier]");
    }

    // 检查相册（Photo Library）权限是否已授权，返回 true 表示已授权
    bool checkPhotoPermission() {
        PHAuthorizationStatus status;
        if (@available(iOS 14, *)) {
            status = [PHPhotoLibrary authorizationStatusForAccessLevel:PHAccessLevelReadWrite];
        } else {
            status = [PHPhotoLibrary authorizationStatus];
        }
        return status == PHAuthorizationStatusAuthorized || status == PHAuthorizationStatusLimited;
    }

    // 检查相机（Camera）权限是否已授权，返回 true 表示已授权
    bool checkCameraPermission() {
        AVAuthorizationStatus status = [AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
        return status == AVAuthorizationStatusAuthorized;
    }

    void showAlertDialog(const char* text, const char* caption, const char* button) {
        NSString *nsText = [NSString stringWithUTF8String:text];
        NSString *nsCaption = [NSString stringWithUTF8String:caption];
        NSString *nsButton = [NSString stringWithUTF8String:button];
        
        dispatch_async(dispatch_get_main_queue(), ^{
            UIAlertController *alert = [UIAlertController alertControllerWithTitle:nsCaption
                                                                         message:nsText
                                                                  preferredStyle:UIAlertControllerStyleAlert];
            
            [alert addAction:[UIAlertAction actionWithTitle:nsButton
                                                      style:UIAlertActionStyleDefault
                                                    handler:nil]];
            
//            [UnityGetGLViewController() presentViewController:alert animated:YES completion:nil];
        });
    }
}
