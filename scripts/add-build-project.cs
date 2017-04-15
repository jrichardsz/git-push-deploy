//@auth
//@req(name, url, branch, targetEnv, login, password, deployType, mountPath)

//git repo url normalization
url = url.replace(/^\s+|\s+$/gm, '');
var ind = url.lastIndexOf(".git");
if (ind == -1 || ind != url.length - 4) {
   ind = url.lastIndexOf("/");
   if (ind > -1 && ind == url.length - 1) url = url.substring(0, ind);
   url += ".git";
}

targetEnv = targetEnv.toString().split(".")[0];
var params = {
   name: name,
   envName: "${env.envName}",
   env: targetEnv,
   nodeId: "${nodes.build.first.id}",
   session: session,
   type: "git",
   context: "ROOT",
   url: url,
   branch: branch,
   keyId: null,
   login: login,
   password: password,
   autoupdate: false,
   interval: 1,
   autoResolveConflict: true
}

//remove the old project
var projectId = parseInt("${nodes.build.first.customitem.projects[0].id}", 10);
if (!isNaN(projectId)) {
   var resp = jelastic.env.build.RemoveProject(params.envName, params.session, params.nodeId, projectId);
   if (resp.result != 0) return resp;
}

//add and build new project 
var resp = jelastic.env.build.AddProject(params.envName, params.session, params.nodeId, params.name, params.type, params.url, params.keyId, params.login, params.password, params.env, params.context, params.branch, params.autoupdate, params.interval, params.autoResolveConflict);
if (resp.result != 0) return resp;
projectId = resp.id;

var module = "/usr/lib/jelastic/modules/maven.module";
var host = window.location.host.replace(/cs|app/, "core");

if (deployType == "mount"){
   //copy app archive to mountPath
   var cmd = [
     'cmd="cp \\${APPROOT}/\\${PROJECT_NAME}/target/*.* '+ mountPath + '/' + params.context +'.war >> \\${LOG_DIR}/\\${PROJECT_NAME}_build.log; writeJSONResponseOut \\"result=>0\\" \\"message=>redirect->build+auto-deploy\\"; return 0; "', 
     'sed -i "/auto-deploy/d" ' + module, 
     'sed -i "/Build success/a  $cmd" ' + module
   ];
   resp = execCmd(params.envName, params.session, params.nodeId, cmd);
   if (resp.result != 0) return resp;
   
   //just build for deploy via mount
   resp = jelastic.env.build.BuildProject(params.envName, params.session, params.nodeId, projectId);
} else {
   //--- temporary fix to JE-31670
   var cmd = ['url="https://' + host + '/JElastic/environment/build/rest/builddeploy?envName=\\$ENVIRONMENT&projectName=\\$PROJECT_NAME"', 
     'cmd="parseArguments \\"\\$@\\"; [[ \\${SESSION:0:4} = \'lds:\' ]] && { readProjectConfig; echo \\$(curl -fsSL \\"$url\\"); writeJSONResponseOut \\"result=>0\\" \\"message=>redirect->build+deploy\\"; return 0; }"', 
     'sed -i "/SESSION:0:/d" ' + module, 'sed -i "/doBuild()/a  $cmd" ' + module];
   resp = execCmd(params.envName, params.session, params.nodeId, cmd);
   if (resp.result != 0) return resp;
   //---
   
   var delay = getParam("delay") || 30;
   resp = jelastic.env.build.BuildDeployProject(params.envName, params.session, params.nodeId, projectId, delay);
}

return resp;

function execCmd(envName, session, nodeId, cmd){
   return jelastic.env.control.ExecCmdById(envName, session, nodeId, toJSON([{
      "command": cmd.join("\n")
   }]) + "", true, "root");
}
