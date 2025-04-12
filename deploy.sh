ModName="TimeoutLimit"

# function for xml reading
read_dom () {
    local IFS=\>
    read -d \< ENTITY CONTENT
}

# read install folder from environment
while read_dom; do
	if [[ $ENTITY = "VALHEIM_INSTALL" ]]; then
		VALHEIM_INSTALL=$CONTENT
	fi
	if [[ $ENTITY = "R2MODMAN_INSTALL" ]]; then
		R2MODMAN_INSTALL=$CONTENT
	fi
	if [[ $ENTITY = "USE_R2MODMAN_AS_DEPLOY_FOLDER" ]]; then
		USE_R2MODMAN_AS_DEPLOY_FOLDER=$CONTENT
	fi
	if [[ $ENTITY = "DEPLOY_FOLDER" ]]; then
    DEPLOY_FOLDER=$CONTENT
  fi
done < Environment.props

# set ModDir
if $USE_R2MODMAN_AS_DEPLOY_FOLDER; then
  BepInExFolder="$R2MODMAN_INSTALL/BepInEx"
else
	BepInExFolder="$VALHEIM_INSTALL/BepInEx"
fi

PluginFolder="$DEPLOY_FOLDER/BepInEx/plugins"
ModDir="$PluginFolder/$ModName"
DedicatedDir="/e/Programme/Steam/steamapps/common/Valheim dedicated server/BepInEx/plugins/$ModName"

# copy content
mkdir -p "$ModDir"
echo "Copying to $ModDir"
cp "$ModName/bin/Debug/$ModName.dll" "$ModDir"
mkdir -p "$DedicatedDir"
echo "Copying to $DedicatedDir"
cp "$ModName/bin/Debug/$ModName.dll" "$DedicatedDir"
cp README.md "$ModDir"
cp manifest.json "$ModDir"
cp "$ModName/bin/Debug/icon.png" "$ModDir"

# make zip files
cd "$ModDir" || exit

[ -f "$ModName.zip" ] && rm "$ModName.zip"
[ -f "$ModName-Nexus.zip" ] && rm "$ModName-Nexus.zip"

mkdir -p plugins
cp "$ModName.dll" plugins

zip "$ModName.zip" "$ModName.dll" README.md manifest.json icon.png
zip -r "$ModName-Nexus.zip" plugins

rm -r plugins
