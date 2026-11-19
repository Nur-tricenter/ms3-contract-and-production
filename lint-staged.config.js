// lint-staged: corre el linter solo sobre los archivos que están en stage.
// Lo dispara .husky/pre-commit. Si el formato no cumple el .editorconfig
// (tabs en los .cs), dotnet format termina con código != 0 y el commit se cancela.
const path = require('node:path');

// lint-staged entrega rutas absolutas; dotnet format --include espera rutas
// relativas a la raíz del repositorio.
const relativos = (archivos) =>
	archivos.map((archivo) => `"${path.relative(process.cwd(), archivo)}"`).join(' ');

module.exports = {
	'*.cs': (archivos) =>
		`dotnet format whitespace Nurtricenter.MS3.sln --verify-no-changes --include ${relativos(archivos)}`,
};
