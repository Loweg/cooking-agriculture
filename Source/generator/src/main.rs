use std::collections::HashMap;
use std::fs::File;
use std::io::{self, Write};

const CONTENT_PATH: &str = "../../Patches/";

struct Def {
	class: String,
	name: String,
}

fn write(path: &'static str, data: &String) -> io::Result<()> {
	std::fs::DirBuilder::new().recursive(true).create(CONTENT_PATH).unwrap();
	let mut f = File::create(format!("{CONTENT_PATH}{path}.xml"))?;
	let s = format!("<?xml version=\"1.0\" encoding=\"utf-8\"?>
<!-- This is an automatically generated file. If you are the end user, this is safe to edit. If you are a contributor, please edit the source files instead. -->
<Patch>{data}</Patch>");
	f.write_all(s.as_bytes())?;
	Ok(())
}

fn construct_patch(def: &Def) -> String {
	let mut s = String::from(PATCH_STRING);
	let replace = HashMap::from([
		("{def_class}", &def.class),
		("{def_name}",  &def.name),
	]);
	for r in replace {
		s = s.replace(r.0, r.1);
	}
	s
}

fn main() {
	let defs = defs();
	let mut defs_xml = String::new();
	for def in &defs {
		defs_xml.push_str(&(construct_patch(def) + "\n"));
	}

	write("RuinablePatchesGenerated", &defs_xml).unwrap();
}

const PATCH_STRING: &'static str = "
<Operation Class=\"PatchOperationConditional\">
	<xpath>Defs/{def_class}[defName=\"{def_name}\"]/comps</xpath>
	<nomatch Class=\"PatchOperationAdd\">
		<xpath>Defs/{def_class}[defName=\"{def_name}\"]</xpath>
		<value>
			<comps>
				<li Class=\"CookingAgriculture.CompProperties_Unfreezable\" />
			</comps>
		</value>
	</nomatch>
	<match Class=\"PatchOperationAdd\">
		<xpath>Defs/{def_class}[defName=\"{def_name}\"]/comps</xpath>
		<value>
			<li Class=\"CookingAgriculture.CompProperties_Unfreezable\" />
		</value>
	</match>
</Operation>";

fn defs() -> Vec<Def> {
	vec![
		Def {
			class: String::from("ThingDef"),
			name: String::from("RawBerries"),
		},
	]
}