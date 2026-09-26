// Placeholder for npm scripts that land in a later stage.
const [name = 'this script', stage = 'a later stage'] = process.argv.slice(2);
console.error(`${name}: not yet implemented (${stage})`);
process.exit(1);
