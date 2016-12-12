﻿var JSuploader = function (form, progressbar) {

    jQuery.sha1 = sha1;
    var filename;
    var TargetFile;
    var Id;
    var Checksum;
    var FileName;
    var Description;
    var Uploaded;

    var Private = true;
    var Datatype;
    var Filesize;
    var Userid = null;
    var DataChunks;
    var ObjectInfo = [];
    var ObjectElement = {};
    var theInput;
    var theFile;
    var ChunkElement = {};
    var FileChunk = [];
    var MaxFileSizeMB = 1;
    var BufferChunkSize = MaxFileSizeMB * (1024 * 1024);
    var ReadBuffer_Size = 1024;
    var FileStreamPos = 0;
    var TotalCount;
    var theForm;
    var progress;

        console.log("uploader skapad!");
        theForm = form;
        progress = progressbar;
        //TargetFile = form[0].files;
        //fileinfo = TargetFile.files[0];
        //filename = fileinfo.name;
        //Checksum = GetSHA1(TargetFile, CarryOn);


    ////Läser av hur många chunks som behöver skickas beroende på vad vi sätter för storleksgräns.
    //function theSizeOfChunks(TargetFile) {
    //    var file = TargetFile[0];
    //    var Size = file.size;
    //    var MaxFileSizeMB = 1;
    //    var BufferChunkSize = MaxFileSizeMB * (1024 * 1024);
    //    var TotalParts = Math.ceil(Size / BufferChunkSize);
    //    return TotalParts;
    //}
    //Läser av storleken på filen
    function theSizeOfFile(TargetFile) {
        console.log(TargetFile);
        var theSize = TargetFile[0].size;
        return theSize;
    }

    //Läser checksum för filen som sedan skickas iväg som metadata.
    function GetSHA1(TargetFile, CarryOnCallback) {
        console.log(TargetFile);
        var file = TargetFile[0];
        var reader = new FileReader();
        reader.onload = function (event) {
            var binary = event.target.result;
            var hashCode = $.sha1(binary);
            CarryOnCallback(hashCode);
        };
        reader.readAsArrayBuffer(file);
        console.log("Loading");
    }

    //Får filändelsen av den valda filen, ex .pdf, .exe, .jpg..
    function GetFileExtension(filename) {
        return filename.split('.').pop();
    }

        //Exekveras när man trycker på upload
    JSuploader.prototype.UploadMyFile = function () {
        TargetFile = theForm[0].files;
        fileinfo = theForm[0].files[0];
        filename = fileinfo.name;
        Checksum = GetSHA1(TargetFile, CarryOn);
    }



    //Funktionen kallas på när checksum är OK, skapar ett objekt av fil-elementen.
    function CarryOn(hashCode) {

        ObjectElement.id = Id;
        ObjectElement.checksum = hashCode;
        ObjectElement.fileName = filename;
        ObjectElement.uploaded = Uploaded;
        Datatype = GetFileExtension(filename);
        ObjectElement.dataType = Datatype;
        Filesize = theSizeOfFile(TargetFile);
        console.log(Filesize);
        ObjectElement.fileSize = Filesize;
        ObjectElement.userId = Userid;
        DataChunks = null;
        ObjectElement.dataChunks = DataChunks;
        theInput = JSON.stringify(ObjectElement);
        console.log(theInput);

        SendData(theInput);
        return false;
    };


    //Här skickar vi metadatan och får tillbaks data.
    function SendData(theInput) {

        var FD = theInput;
        $.ajax({
            type: "POST",
            //url: 'http://localhost:56875/api/v1.0/FileItems/',
            url: '../api/v1.0/FileItems/',
            contentType: 'application/json',
            dataType: 'json',
            data: FD,
            error: function (e) {
                console.log(e);
            },
            //Är det ok, så påbörjar vi metoden med att skicka datachunks av filen.
            success: function (result, status, jqHXR) {
                var jsonUpdateData = result;
                Datatype: "json",
                console.log("Första");
                UploadFile(jsonUpdateData, TargetFile);
            }
        });
    };

    //Delar upp filen, namnger den och skickar den vidare-
    //till reader för att få de sista elementen innan det skickas.
    function UploadFile(jsonUpdateData, TargetFile) {

        var FileID = jsonUpdateData.id;
        //var fileitemlist = null;
        var file = TargetFile[0];
        var EndPos = BufferChunkSize;
        var Size = file.size;

        while (FileStreamPos < Size) {
            FileChunk.push(file.slice(FileStreamPos, EndPos));
            FileStreamPos = EndPos; // Hoppar för varje läst fil.
            EndPos = FileStreamPos + BufferChunkSize; // sätter nästa chunk-längd.

        }
        TotalCount = FileChunk.length;
        var PartCount = 0;
        SendNextPart(FileChunk, file, FileID, PartCount);
    };
    function SendNextPart(FileChunk, file, FileID, PartCount) {

        var chunk = FileChunk.shift();
        if (chunk == null) { return; }
        PartCount++;

        blob = new Blob([chunk], { type: 'application/octet-binary' });


        var promise = new Promise(ReadingTheBytesAndCheckSum);
        promise.then(function (data) {


            var FilePartName = file.name + ".part_" + PartCount + "." + TotalCount;
            var byteData = data.byteArray;

            var ChunkElement = {};

            ChunkElement.CheckSum = data.hashCode2;
            ChunkElement.PartName = FilePartName;
            ChunkElement.Data = byteData;
            ChunkElement.FileItemId = FileID;
            var FD = new FormData();


            FD.append('PartName', FilePartName);
            FD.append('Checksum', data.hashCode2);
            FD.append('Data', chunk);
            FD.append('FileItemId', FileID);

            console.log(ChunkElement);
            $.ajax({
                type: "POST",
                //url: 'http://localhost:56875/api/v1.0/FileItems/' + FileID,
                url: '../api/v1.0/FileItems/' + FileID,
                //contentType: 'multipart/form-data; boundary = --boundary--',
                contentType: false,
                processData: false,

                data: FD,
                error: function (e) {
                    console.log(e);
                },
                success: function (result, status, jqHXR) {
                    var jsonUpdateData = result;
                    //Datatype: "json";
                    Datatype: false;
                    SendNextPart(FileChunk, file, FileID, PartCount++)
                }
            });
            console.log("Done");
        });
        //Läser checksum och binär data för chunken
        function ReadingTheBytesAndCheckSum(resolve) {

            var reader = new FileReader();
            reader.onload = function (event) {
                var binary = event.target.result;
                var bytes = new Uint8Array(binary);
                var byteArray = [].slice.call(bytes);
                var hashCode2 = $.sha1(binary);
                var theObject = { byteArray, hashCode2};
                resolve(theObject);
            };
            reader.readAsArrayBuffer(blob);
        }
        console.log("still testing");
    }
}









