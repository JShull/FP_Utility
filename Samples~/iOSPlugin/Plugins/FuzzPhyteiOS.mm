#import <UIKit/UIKit.h>
#import <Foundation/Foundation.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#import <ifaddrs.h>
#import <arpa/inet.h>
#import <string.h>
#import "UnityInterface.h"

// combines ExportToFiles.mm, IPAddress.mm, and MarkdownImportHelper.mm functionality
#pragma mark - File Export Helper

@interface FileExportHelper : NSObject <UIDocumentPickerDelegate>
@property (nonatomic, strong) NSString *fileType;
@end

@implementation FileExportHelper

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls {
    NSLog(@"File Export Picker Completed");
    UnitySendMessage("UnityIOSCallback", "OnFilePickerClosed", [self.fileType UTF8String]);
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller {
    NSLog(@"File Export Picker Cancelled");
    UnitySendMessage("UnityIOSCallback", "OnFilePickerClosed", [self.fileType UTF8String]);
}

@end

static FileExportHelper *exportHelper = nil;

#pragma mark - Markdown Import Helper

@interface MarkdownImportHelper : NSObject <UIDocumentPickerDelegate>
@end

@implementation MarkdownImportHelper

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls {
    NSURL *url = urls.firstObject;
    NSData *fileData = [NSData dataWithContentsOfURL:url];
    NSString *fileString = [[NSString alloc] initWithData:fileData encoding:NSUTF8StringEncoding];

    if (!fileString) {
        fileString = @"[Error: Could not decode .md file]";
    }

    UnitySendMessage("UnityIOSCallback", "OnMarkdownFileLoaded", [fileString UTF8String]);
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller {
    UnitySendMessage("UnityIOSCallback", "OnMarkdownFileLoaded", "[Cancelled]");
}

@end

static MarkdownImportHelper *markdownHelper = nil;

#pragma mark - C Interface

extern "C" {

// Export JSON file to Files app
void ShowiOSFileSaveDialog(const char *jsonText, const char *fileName) {
    NSString *content = [NSString stringWithUTF8String:jsonText];
    NSString *name = [NSString stringWithUTF8String:fileName];
    NSData *data = [content dataUsingEncoding:NSUTF8StringEncoding];

    NSURL *tempDir = [NSURL fileURLWithPath:NSTemporaryDirectory()];
    NSURL *fileURL = [tempDir URLByAppendingPathComponent:name];

    [data writeToURL:fileURL atomically:YES];

    UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initForExportingURLs:@[fileURL]];
    picker.modalPresentationStyle = UIModalPresentationFormSheet;

    exportHelper = [FileExportHelper new];
    exportHelper.fileType = @"json";
    picker.delegate = exportHelper;

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *rootVC = UIApplication.sharedApplication.keyWindow.rootViewController;
        [rootVC presentViewController:picker animated:YES completion:nil];
    });
}

// Export WAV file to Files app
void ShowiOSWavSaveDialog(const void *wavData, int dataLength, const char *fileName) {
    NSString *name = [NSString stringWithUTF8String:fileName];
    NSData *data = [NSData dataWithBytes:wavData length:dataLength];

    NSURL *tempDir = [NSURL fileURLWithPath:NSTemporaryDirectory()];
    NSURL *fileURL = [tempDir URLByAppendingPathComponent:name];

    [data writeToURL:fileURL atomically:YES];

    UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initForExportingURLs:@[fileURL]];
    picker.modalPresentationStyle = UIModalPresentationFormSheet;

    exportHelper = [FileExportHelper new];
    exportHelper.fileType = @"wav";
    picker.delegate = exportHelper;

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *rootVC = UIApplication.sharedApplication.keyWindow.rootViewController;
        [rootVC presentViewController:picker animated:YES completion:nil];
    });
}

// Open Markdown file and send contents back to Unity
void ShowiOSMarkdownImportDialog() {
    UTType *markdownType = [UTType typeWithFilenameExtension:@"md"];
    UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:@[markdownType]];
    picker.modalPresentationStyle = UIModalPresentationFormSheet;
    picker.allowsMultipleSelection = NO;

    markdownHelper = [MarkdownImportHelper new];
    picker.delegate = markdownHelper;

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *rootVC = UIApplication.sharedApplication.keyWindow.rootViewController;
        [rootVC presentViewController:picker animated:YES completion:nil];
    });
}

// Get WiFi IP Address (en0 interface)
const char * _GetWiFiIPAddress() {
    static char addressBuffer[INET_ADDRSTRLEN] = "0.0.0.0";

    struct ifaddrs *interfaces = NULL;
    struct ifaddrs *temp_addr = NULL;

    if (getifaddrs(&interfaces) == 0) {
        temp_addr = interfaces;
        while (temp_addr != NULL) {
            if (temp_addr->ifa_addr && temp_addr->ifa_addr->sa_family == AF_INET) {
                if (strcmp(temp_addr->ifa_name, "en0") == 0) {
                    struct sockaddr_in *addr = (struct sockaddr_in *)temp_addr->ifa_addr;
                    if (addr) {
                        inet_ntop(AF_INET, &(addr->sin_addr), addressBuffer, INET_ADDRSTRLEN);
                        break;
                    }
                }
            }
            temp_addr = temp_addr->ifa_next;
        }
    }

    if (interfaces != NULL) {
        freeifaddrs(interfaces);
    }

    return addressBuffer;
}

}
