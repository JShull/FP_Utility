// Copyright (c) 2026 John B. Shull.
// See LICENSE.md.

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

@interface FPFileExportDelegate : NSObject <UIDocumentPickerDelegate>
@property(nonatomic, strong) NSURL *temporaryFileURL;
@end

@implementation FPFileExportDelegate

- (void)cleanupTemporaryFile
{
    if (self.temporaryFileURL != nil)
    {
        [[NSFileManager defaultManager] removeItemAtURL:self.temporaryFileURL error:nil];
        self.temporaryFileURL = nil;
    }
}

- (void)documentPicker:(UIDocumentPickerViewController *)controller
    didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
    [self cleanupTemporaryFile];
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
    [self cleanupTemporaryFile];
}

@end

static FPFileExportDelegate *FPSharedFileExportDelegate = nil;

static UIViewController *FPTopViewController(void)
{
    UIWindow *window = nil;
    for (UIWindow *candidate in [UIApplication sharedApplication].windows)
    {
        if (candidate.isKeyWindow)
        {
            window = candidate;
            break;
        }
    }
    if (window == nil)
    {
        window = [UIApplication sharedApplication].windows.firstObject;
    }

    UIViewController *controller = window.rootViewController;
    while (controller.presentedViewController != nil)
    {
        controller = controller.presentedViewController;
    }
    return controller;
}

extern "C" void FP_SaveBytesToFiles(
    const void *bytes,
    int dataLength,
    const char *fileName)
{
    if (bytes == NULL || dataLength <= 0 || fileName == NULL)
    {
        return;
    }

    NSData *data = [NSData dataWithBytes:bytes length:(NSUInteger)dataLength];
    NSString *name = [NSString stringWithUTF8String:fileName];
    NSURL *temporaryURL = [[NSURL fileURLWithPath:NSTemporaryDirectory()]
        URLByAppendingPathComponent:name];
    if (![data writeToURL:temporaryURL atomically:YES])
    {
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        UIDocumentPickerViewController *picker;
        if (@available(iOS 14.0, *))
        {
            picker = [[UIDocumentPickerViewController alloc]
                initForExportingURLs:@[temporaryURL]
                asCopy:YES];
        }
        else
        {
            picker = [[UIDocumentPickerViewController alloc]
                initWithURL:temporaryURL
                inMode:UIDocumentPickerModeExportToService];
        }

        FPSharedFileExportDelegate = [FPFileExportDelegate new];
        FPSharedFileExportDelegate.temporaryFileURL = temporaryURL;
        picker.delegate = FPSharedFileExportDelegate;
        UIViewController *controller = FPTopViewController();
        if (controller != nil)
        {
            [controller presentViewController:picker animated:YES completion:nil];
        }
        else
        {
            [FPSharedFileExportDelegate cleanupTemporaryFile];
        }
    });
}
