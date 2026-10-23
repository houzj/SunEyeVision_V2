const fs=require('fs'),path=require('path');
module.exports=function(file,data){
    try { fs.writeFileSync(file,data); }
    catch(error){
        const root=path.resolve('docs')+path.sep,target=path.resolve(file);
        if(error.code!=='UNKNOWN'||!target.startsWith(root)||!fs.existsSync(file))throw error;
        // A Windows reader can deny opening an existing file with truncation while
        // permitting read/write access. Keep the same document and replace its bytes.
        const bytes=Buffer.isBuffer(data)?data:Buffer.from(data,'utf8');
        const fd=fs.openSync(file,'r+');
        try { fs.writeFileSync(fd,bytes);fs.ftruncateSync(fd,bytes.length); }
        finally { fs.closeSync(fd); }
    }
};
